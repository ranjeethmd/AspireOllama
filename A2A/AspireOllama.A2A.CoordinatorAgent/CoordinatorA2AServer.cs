using AspireOllama.A2A.Shared;
using AspireOllama.ServiceDefaults.Authentication;
using OllamaSharp;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AspireOllama.A2A.CoordinatorAgent;

/// <summary>
/// Coordinator Agent: AI-driven multi-agent workflow orchestration.
/// The LLM decides which agents to call, in what order, and what to ask.
/// The coordinator only provides the execution framework — the AI drives the plan.
/// </summary>
public class CoordinatorA2AServer(
    Lazy<IOllamaApiClient> ollamaClient,
    IA2AAgentClient agentClient,
    ILogger<CoordinatorA2AServer> logger) : A2AServerBase(logger, agentClient)
{
    private const int MaxRetries = 1;
    private const int SubtaskTimeoutSeconds = 300; // 5 min per subtask
    private const int MaxPlanSteps = 5;            // Cap LLM plan to 5 steps
    private const int MaxLoopIterations = 6;       // Safety: max loop cycles
    private static readonly string[] AvailableAgents = ["planner", "reviewer", "research", "code"];

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public override AgentCard GetAgentCard() => new()
    {
        Name = "Coordinator Agent",
        Description = "AI-driven multi-agent workflow orchestration. The LLM decides which agents to call and in what order.",
        Version = "2.0.0",
        Url = "http://coordinator-agent",
        Skills =
        [
            new AgentSkill
            {
                Id = "orchestrate_task",
                Name = "Orchestrate Task",
                Description = "Accepts a complex task, uses AI to plan and execute a dynamic multi-agent workflow",
                Tags = ["orchestration", "coordination", "multi-agent", "workflow"]
            }
        ]
    };

    public override string? ResolveSkill(A2AMessage message) => "orchestrate_task";

    public override IReadOnlyDictionary<string, string> GetSkillRoles()
        => AuthRoles.A2ASkillRoles.GetValueOrDefault("coordinator") ?? new Dictionary<string, string>();

    public override async Task<A2ATask> ProcessMessageAsync(A2AMessage message, CancellationToken ct)
    {
        var task = CreateTask(message);
        var userRequest = GetTextFromMessage(message);
        if (string.IsNullOrWhiteSpace(userRequest))
        {
            UpdateTaskStatus(task, TaskState.Failed, "No task description provided");
            return task;
        }

        logger.LogInformation("Coordinator starting AI-driven orchestration for: {Task}", userRequest);
        var totalSw = Stopwatch.StartNew();
        var trace = new List<WorkflowStep>();
        var stepCounter = 0;
        var context = new Dictionary<string, string>(); // accumulates results for later steps

        try
        {
            // ── Step 1: Ask the LLM to create an execution plan ──
            UpdateTaskStatus(task, TaskState.Working, "Planning workflow...", 5);
            var planSw = Stopwatch.StartNew();

            var planPrompt = $@"Create an execution plan as a JSON array. Each element is one step.

Available agents and their skills:

PLANNER agent:
- create_plan: Creates detailed step-by-step plans for complex tasks
- assess_complexity: Evaluates task complexity (low/medium/high) and identifies requirements
- suggest_agents: Recommends which agents should handle components of a task

RESEARCH agent:
- search_knowledge: Searches knowledge base for relevant information
- get_topic_details: Gets detailed information about a specific topic
- gather_context: Gathers broad context and background information for a task
- suggest_topics: Suggests related topics worth exploring

CODE agent:
- execute_csharp: Executes C# code snippets and returns results
- generate_code: Generates code based on requirements and specifications
- analyze_code: Analyzes existing code for patterns, issues, and improvements
- generate_tests: Generates unit tests for given code
- refactor_code: Refactors code for better quality, readability, and performance

REVIEWER agent:
- review_response: Reviews any response for accuracy and completeness
- review_code: Reviews code for bugs, security issues, and best practices
- review_plan: Reviews plans for feasibility and completeness
- provide_feedback: Provides constructive feedback on any work product

Each step has these fields:
- ""agent"": one of planner, research, code, reviewer
- ""action"": one of the skill IDs listed above for that agent
- ""instruction"": detailed prompt for the agent (be specific)
- ""dependsOn"": array of 1-based step positions this step needs ([] if independent)

Example:
[
  {{""agent"":""research"",""action"":""gather_context"",""instruction"":""Research best practices for..."",""dependsOn"":[]}},
  {{""agent"":""code"",""action"":""generate_code"",""instruction"":""Generate code that..."",""dependsOn"":[1]}},
  {{""agent"":""reviewer"",""action"":""review_code"",""instruction"":""Review the code for..."",""dependsOn"":[2]}}
]

Rules:
- Use the exact skill IDs listed above for the action field
- Steps with empty dependsOn run in parallel
- dependsOn uses 1-based positions (first item = 1)
- End with a reviewer step to validate the work
- Return ONLY the JSON array

Task: {userRequest}";

            var plan = await AskLlm(planPrompt, ct);

            planSw.Stop();

            var steps = DeserializePlan(plan);
            // Cap plan size, assign step numbers, clamp deps
            if (steps.Count > MaxPlanSteps)
            {
                logger.LogWarning("Plan has {Count} steps, capping to {Max}", steps.Count, MaxPlanSteps);
                steps = steps.Take(MaxPlanSteps).ToList();
            }
            for (int i = 0; i < steps.Count; i++)
            {
                steps[i].Step = i + 1;
                steps[i].DependsOn = steps[i].DependsOn.Where(d => d > 0 && d < i + 1).Distinct().ToList();
            }
            if (steps.Count == 0)
            {
                // Fallback: if LLM failed to produce valid JSON, create a simple plan
                steps =
                [
                    new() { Step = 1, Agent = "research", Action = "gather_context", Instruction = $"Gather relevant information for: {userRequest}", DependsOn = [] },
                    new() { Step = 2, Agent = "planner", Action = "create_plan", Instruction = $"Create a detailed plan for: {userRequest}", DependsOn = [] },
                    new() { Step = 3, Agent = "code", Action = "generate_code", Instruction = $"Implement the solution for: {userRequest}", DependsOn = [1, 2] },
                    new() { Step = 4, Agent = "reviewer", Action = "review", Instruction = $"Review all results for: {userRequest}", DependsOn = [1, 2, 3] }
                ];
                logger.LogWarning("Failed to parse LLM plan, using fallback plan with {Count} steps", steps.Count);
            }

            trace.Add(new WorkflowStep(++stepCounter, "coordinator", "plan",
                "completed", planSw.ElapsedMilliseconds,
                $"Created plan with {steps.Count} steps: {string.Join(" → ", steps.Select(s => $"{s.Agent}/{s.Action}"))}"));
            AddTextArtifact(task, JsonSerializer.Serialize(steps, JsonOpts), "execution_plan");

            // ── Execute steps respecting dependencies ──
            var completed = new HashSet<int>();
            var stepResults = new Dictionary<int, string>();
            var totalSteps = steps.Count;

            var loopCount = 0;
            while (completed.Count < steps.Count && loopCount++ < MaxLoopIterations)
            {
                // Find steps whose dependencies are all completed
                var ready = steps
                    .Where(s => !completed.Contains(s.Step) && s.DependsOn.All(d => completed.Contains(d)))
                    .ToList();

                if (ready.Count == 0)
                {
                    var remaining = steps.Where(s => !completed.Contains(s.Step)).ToList();
                    logger.LogWarning("Deadlock detected — {Count} remaining steps have unmet dependencies: {Steps}",
                        remaining.Count,
                        string.Join(", ", remaining.Select(s => $"step {s.Step} ({s.Agent}/{s.Action}) needs [{string.Join(",", s.DependsOn)}]")));

                    // Force execute remaining steps sequentially to avoid silent failure
                    foreach (var stuck in remaining)
                    {
                        logger.LogInformation("Force executing stuck step {Step}: {Agent}/{Action}", stuck.Step, stuck.Agent, stuck.Action);
                        var (resultText, ms) = await CallAgentTracked(stuck.Agent, stuck.Instruction, ct);
                        completed.Add(stuck.Step);
                        stepResults[stuck.Step] = resultText;
                        context[stuck.Agent] = resultText;

                        trace.Add(new WorkflowStep(++stepCounter, stuck.Agent, stuck.Action, "completed (forced)", ms, resultText));
                        AddTextArtifact(task, resultText, $"step{stuck.Step}_{stuck.Agent}_{stuck.Action}");
                    }
                    break;
                }

                var progress = (int)(25 + (70.0 * completed.Count / totalSteps));
                var readyNames = string.Join(", ", ready.Select(s => $"{s.Agent}/{s.Action}"));
                UpdateTaskStatus(task, TaskState.Working,
                    $"Executing: {readyNames} ({completed.Count}/{totalSteps} done)", progress);

                // Build instructions with context from dependencies
                var parallelWork = ready.Select(step =>
                {
                    var instruction = step.Instruction;

                    // Inject results from dependent steps
                    if (step.DependsOn.Count > 0)
                    {
                        var depContext = string.Join("\n\n", step.DependsOn
                            .Where(d => stepResults.ContainsKey(d))
                            .Select(d =>
                            {
                                var depStep = steps.First(s => s.Step == d);
                                return $"[Result from {depStep.Agent}/{depStep.Action}]:\n{stepResults[d]}";
                            }));

                        if (!string.IsNullOrWhiteSpace(depContext))
                        {
                            instruction = $"Context from previous steps:\n{depContext}\n\nYour task: {instruction}";
                        }
                    }

                    return (step, work: CallAgentTracked(step.Agent, instruction, ct));
                }).ToList();

                // Execute in parallel
                await Task.WhenAll(parallelWork.Select(p => p.work));

                foreach (var (step, work) in parallelWork)
                {
                    var (resultText, ms) = await work;
                    completed.Add(step.Step);
                    stepResults[step.Step] = resultText;
                    context[step.Agent] = resultText;

                    trace.Add(new WorkflowStep(++stepCounter, step.Agent, step.Action, "completed", ms, resultText, step.DependsOn));
                    AddTextArtifact(task, resultText, $"step{step.Step}_{step.Agent}_{step.Action}");

                    logger.LogInformation("Step {Step} ({Agent}/{Action}) completed in {Time}ms",
                        step.Step, step.Agent, step.Action, ms);
                }
            }

            // Conflict resolution removed — reviewer feedback is included in aggregation.

            // ── Final aggregation ──
            UpdateTaskStatus(task, TaskState.Working, "Aggregating results...", 92);
            var aggSw = Stopwatch.StartNew();

            var allResults = string.Join("\n\n", trace
                .Where(t => t.Agent != "coordinator")
                .Select(t => $"## {t.Agent} / {t.Action}:\n{t.Result}"));

            var aggregated = await AskLlm($"""
                Synthesize results from a multi-agent workflow into a clear response.

                User's task: {userRequest}

                Agent results (including any revisions from conflict resolution):
                {allResults}

                Provide a comprehensive response. Include code if generated. Cite agents when relevant.
                If issues were found and resolved, mention the improvements made.
                """, ct);

            aggSw.Stop();
            trace.Add(new WorkflowStep(++stepCounter, "coordinator", "aggregate", "completed", aggSw.ElapsedMilliseconds, aggregated));

            totalSw.Stop();

            // Store trace and final response
            AddTextArtifact(task, JsonSerializer.Serialize(trace, JsonOpts), "workflow_trace");
            AddTextArtifact(task, aggregated, "final_response");
            AddResponseToHistory(task, aggregated);
            UpdateTaskStatus(task, TaskState.Completed,
                $"Completed: {trace.Count} steps, {trace.Select(t => t.Agent).Distinct().Count()} agents, {totalSw.ElapsedMilliseconds}ms", 100);
        }
        catch (Exception ex)
        {
            totalSw.Stop();
            trace.Add(new WorkflowStep(++stepCounter, "coordinator", "error", "failed", totalSw.ElapsedMilliseconds, ex.Message));
            AddTextArtifact(task, JsonSerializer.Serialize(trace, JsonOpts), "workflow_trace");

            logger.LogError(ex, "Coordinator orchestration failed");
            UpdateTaskStatus(task, TaskState.Failed, $"Orchestration failed: {ex.Message}");
        }

        return task;
    }

    /// <summary>
    /// Parses the LLM's JSON plan into structured steps.
    /// Renumbers steps sequentially (LLMs often return all zeros) and remaps dependencies.
    /// </summary>
    private List<PlannedStep> DeserializePlan(string llmOutput)
    {
        try
        {
            var cleaned = llmOutput.Replace("```json", "").Replace("```", "").Trim();
            return JsonSerializer.Deserialize<List<PlannedStep>>(cleaned, JsonOpts) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deserialize plan JSON");
            return [];
        }
    }

    private async Task<string> AskLlm(string prompt, CancellationToken ct)
    {
        var chat = new OllamaSharp.Models.Chat.ChatRequest
        {
            Model = ollamaClient.Value.SelectedModel,
            Messages = [new OllamaSharp.Models.Chat.Message { Role = "user", Content = prompt }]
        };

        var result = "";
        await foreach (var chunk in ollamaClient.Value.ChatAsync(chat, ct))
        {
            if (chunk?.Message?.Content is not null)
                result += chunk.Message.Content;
        }
        return result;
    }

    private async Task<(string result, long elapsedMs)> CallAgentTracked(
        string agentName, string message, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var response = await CallAgentWithRetryAsync(agentName, message, ct);
        sw.Stop();
        return (GetResultText(response), sw.ElapsedMilliseconds);
    }

    private async Task<SendMessageResponse?> CallAgentWithRetryAsync(
        string agentName, string message, CancellationToken ct)
    {
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(SubtaskTimeoutSeconds));

                var response = await CallAgentAsync(agentName, message, timeoutCts.Token);
                if (response?.Task?.Status?.State == TaskState.Completed)
                    return response;

                if (response?.Task?.Status?.State == TaskState.Failed && attempt < MaxRetries)
                {
                    logger.LogWarning("Agent {Agent} failed (attempt {Attempt}), retrying...", agentName, attempt + 1);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                    continue;
                }
                return response;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("Agent {Agent} timed out (attempt {Attempt})", agentName, attempt + 1);
                if (attempt < MaxRetries) continue;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error calling agent {Agent} (attempt {Attempt})", agentName, attempt + 1);
                if (attempt < MaxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                    continue;
                }
            }
        }
        return null;
    }

    private static string GetResultText(SendMessageResponse? response)
    {
        if (response is null) return "(no response)";
        if (response.Task?.Artifacts?.Count > 0)
        {
            var text = GetTextFromArtifact(response.Task.Artifacts[^1]);
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }
        if (response.Task?.History?.Count > 0)
        {
            var lastAgent = response.Task.History.LastOrDefault(h => h.Role == MessageRole.Agent);
            if (lastAgent?.Parts?.Count > 0 && !string.IsNullOrWhiteSpace(lastAgent.Parts[0].Text))
                return lastAgent.Parts[0].Text;
        }
        return response.Task?.Status?.Message ?? "(empty response)";
    }
}

// ── Models ──

/// <summary>
/// A step in the LLM-generated execution plan.
/// Deserialized from the JSON array produced by the planning prompt.
/// </summary>
public class PlannedStep
{
    [JsonPropertyName("step")]
    public int Step { get; set; }

    [JsonPropertyName("agent")]
    public string Agent { get; set; } = "";

    [JsonPropertyName("action")]
    public string Action { get; set; } = "";

    [JsonPropertyName("instruction")]
    public string Instruction { get; set; } = "";

    [JsonPropertyName("dependsOn")]
    public List<int> DependsOn { get; set; } = [];
}

/// <summary>
/// A completed step in the workflow execution trace.
/// Serialized as JSON in the workflow_trace artifact for the UI.
/// </summary>
public class WorkflowStep
{
    [JsonPropertyName("step")]
    public int Step { get; set; }

    [JsonPropertyName("agent")]
    public string Agent { get; set; } = "";

    [JsonPropertyName("action")]
    public string Action { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("elapsedMs")]
    public long ElapsedMs { get; set; }

    [JsonPropertyName("result")]
    public string Result { get; set; } = "";

    [JsonPropertyName("dependsOn")]
    public List<int>? DependsOn { get; set; }

    public WorkflowStep() { }

    public WorkflowStep(int step, string agent, string action, string status, long elapsedMs, string result, List<int>? dependsOn = null)
    {
        Step = step;
        Agent = agent;
        Action = action;
        Status = status;
        ElapsedMs = elapsedMs;
        Result = result;
        DependsOn = dependsOn;
    }
}
