using System.Text.Json;
using AspireOllama.A2A.Shared;
using OllamaSharp;

namespace AspireOllama.A2A.CoordinatorAgent;

/// <summary>
/// Coordinator Agent: orchestrates complex multi-agent workflows.
/// Accepts a task → plans via Planner → dispatches subtasks in parallel →
/// resolves conflicts via Reviewer → aggregates results via LLM.
/// </summary>
public class CoordinatorA2AServer(
    Lazy<IOllamaApiClient> ollamaClient,
    IA2AAgentClient agentClient,
    ILogger<CoordinatorA2AServer> logger) : A2AServerBase(logger)
{
    private const int MaxRetries = 2;
    private const int SubtaskTimeoutSeconds = 300; // 5 minutes per subtask (large models are slow)

    public override AgentCard GetAgentCard() => new()
    {
        Name = "Coordinator Agent",
        Description = "Orchestrates complex multi-agent workflows. Accepts tasks, plans via Planner, " +
                      "dispatches subtasks in parallel, resolves conflicts, and aggregates results.",
        Version = "1.0.0",
        Url = "http://coordinator-agent",
        Skills =
        [
            new AgentSkill
            {
                Id = "orchestrate_task",
                Name = "Orchestrate Task",
                Description = "Accepts a complex task, plans it, executes subtasks across agents, resolves conflicts, and returns aggregated results",
                Tags = ["orchestration", "coordination", "multi-agent", "workflow"]
            }
        ]
    };

    public override async Task<A2ATask> ProcessMessageAsync(A2AMessage message, CancellationToken ct)
    {
        var task = CreateTask(message);
        var userRequest = GetTextFromMessage(message);
        if (string.IsNullOrWhiteSpace(userRequest))
        {
            UpdateTaskStatus(task, TaskState.Failed, "No task description provided");
            return task;
        }

        logger.LogInformation("Coordinator starting orchestration for: {Task}", userRequest);
        UpdateTaskStatus(task, TaskState.Working, "Phase 1: Assessing task complexity...", 5);

        try
        {
            // ── Phase 1: Assess complexity ──
            var complexity = await CallAgentWithRetryAsync("planner",
                $"Assess the complexity of this task and determine which agents are needed: {userRequest}", ct);
            var complexityText = GetResultText(complexity);

            AddTextArtifact(task, "complexity_assessment", complexityText);
            UpdateTaskStatus(task, TaskState.Working, "Phase 2: Creating execution plan...", 15);

            // ── Phase 2: Create plan ──
            var plan = await CallAgentWithRetryAsync("planner",
                $"Create a detailed step-by-step plan for this task. For each step, specify which agent should handle it (planner, reviewer, research, or code). Task: {userRequest}", ct);
            var planText = GetResultText(plan);

            AddTextArtifact(task, "execution_plan", planText);
            UpdateTaskStatus(task, TaskState.Working, "Phase 3: Executing subtasks...", 25);

            // ── Phase 3: Parallel execution ──
            // Determine which agents to call based on complexity assessment
            var agentResults = new Dictionary<string, string>();
            var lowerComplexity = complexityText.ToLowerInvariant();

            var agentTasks = new List<(string agent, string prompt, Task<SendMessageResponse?> task)>();

            // Research: gather context (run in parallel with code if both needed)
            if (lowerComplexity.Contains("research") || lowerComplexity.Contains("context") || lowerComplexity.Contains("knowledge"))
            {
                var researchTask = CallAgentWithRetryAsync("research",
                    $"Gather relevant information and context for: {userRequest}", ct);
                agentTasks.Add(("research", "gather_context", researchTask));
            }

            // Code: generate or analyze code
            if (lowerComplexity.Contains("code") || lowerComplexity.Contains("programming") || lowerComplexity.Contains("implement"))
            {
                var codeTask = CallAgentWithRetryAsync("code",
                    $"Based on this plan:\n{planText}\n\nGenerate code or technical implementation for: {userRequest}", ct);
                agentTasks.Add(("code", "generate_code", codeTask));
            }

            // Execute parallel tasks
            if (agentTasks.Count > 0)
            {
                logger.LogInformation("Executing {Count} subtasks in parallel", agentTasks.Count);
                await Task.WhenAll(agentTasks.Select(t => t.task));

                foreach (var (agent, prompt, agentTask) in agentTasks)
                {
                    var result = await agentTask;
                    var resultText = GetResultText(result);
                    agentResults[agent] = resultText;
                    AddTextArtifact(task, $"{agent}_result", resultText);
                    logger.LogInformation("Agent {Agent} completed", agent);
                }
            }

            UpdateTaskStatus(task, TaskState.Working, "Phase 4: Reviewing and resolving conflicts...", 65);

            // ── Phase 4: Review and conflict resolution ──
            var allResults = string.Join("\n\n---\n\n", agentResults.Select(r =>
                $"## {r.Key} agent result:\n{r.Value}"));

            var reviewPrompt = agentResults.Count > 1
                ? $"Review the following results from multiple agents for the task: '{userRequest}'\n\n" +
                  $"Plan:\n{planText}\n\n" +
                  $"Results:\n{allResults}\n\n" +
                  "Check for conflicts, errors, or inconsistencies. Provide feedback and a quality assessment."
                : $"Review this result for the task: '{userRequest}'\n\nPlan:\n{planText}\n\n{allResults}\n\nProvide feedback and quality assessment.";

            var review = await CallAgentWithRetryAsync("reviewer", reviewPrompt, ct);
            var reviewText = GetResultText(review);
            AddTextArtifact(task, "review", reviewText);

            // Check if reviewer found conflicts
            var reviewLower = reviewText.ToLowerInvariant();
            if (reviewLower.Contains("conflict") || reviewLower.Contains("inconsisten") || reviewLower.Contains("contradict"))
            {
                logger.LogInformation("Conflicts detected, attempting resolution");
                UpdateTaskStatus(task, TaskState.Working, "Resolving conflicts...", 75);

                // Re-run conflicting agents with reviewer feedback as context
                foreach (var (agent, result) in agentResults)
                {
                    if (reviewText.Contains(agent, StringComparison.OrdinalIgnoreCase))
                    {
                        var retryResult = await CallAgentWithRetryAsync(agent,
                            $"The reviewer found issues with your previous response. " +
                            $"Reviewer feedback: {reviewText}\n\n" +
                            $"Your previous result: {result}\n\n" +
                            $"Please revise your response for: {userRequest}", ct);
                        agentResults[agent] = GetResultText(retryResult);
                        AddTextArtifact(task, $"{agent}_revised", agentResults[agent]);
                    }
                }
            }

            UpdateTaskStatus(task, TaskState.Working, "Phase 5: Aggregating results...", 85);

            // ── Phase 5: Aggregate results via LLM ──
            var aggregationPrompt = $"""
                You are synthesizing results from a multi-agent workflow.

                User's original task: {userRequest}

                Plan:
                {planText}

                Agent results:
                {string.Join("\n\n", agentResults.Select(r => $"## {r.Key}:\n{r.Value}"))}

                Reviewer feedback:
                {reviewText}

                Provide a clear, comprehensive response that addresses the user's original task.
                Synthesize all agent outputs into a coherent answer.
                If agents provided code, include it formatted properly.
                Cite which agent contributed what when relevant.
                """;

            var chat = new OllamaSharp.Models.Chat.ChatRequest
            {
                Model = ollamaClient.Value.SelectedModel,
                Messages = [new OllamaSharp.Models.Chat.Message { Role = "user", Content = aggregationPrompt }]
            };

            var aggregated = "";
            await foreach (var chunk in ollamaClient.Value.ChatAsync(chat, ct))
            {
                if (chunk?.Message?.Content is not null)
                    aggregated += chunk.Message.Content;
            }

            AddTextArtifact(task, "final_response", aggregated);
            AddResponseToHistory(task, aggregated);
            UpdateTaskStatus(task, TaskState.Completed, "Workflow completed successfully", 100);

            logger.LogInformation("Coordinator completed orchestration with {AgentCount} agents", agentResults.Count + 2);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Coordinator orchestration failed");
            UpdateTaskStatus(task, TaskState.Failed, $"Orchestration failed: {ex.Message}");
        }

        return task;
    }

    /// <summary>
    /// Calls an agent with retry and timeout.
    /// </summary>
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
                if (attempt < MaxRetries)
                    continue;
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

    /// <summary>
    /// Extracts text from an agent response.
    /// </summary>
    private static string GetResultText(SendMessageResponse? response)
    {
        if (response is null) return "(no response)";

        // Try artifacts first
        if (response.Task?.Artifacts?.Count > 0)
        {
            var text = GetTextFromArtifact(response.Task.Artifacts[^1]);
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        // Try history
        if (response.Task?.History?.Count > 0)
        {
            var lastAgent = response.Task.History.LastOrDefault(h => h.Role == MessageRole.Agent);
            if (lastAgent?.Parts?.Count > 0 && !string.IsNullOrWhiteSpace(lastAgent.Parts[0].Text))
                return lastAgent.Parts[0].Text;
        }

        return response.Task?.Status?.Message ?? "(empty response)";
    }
}
