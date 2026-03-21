using AspireOllama.Shared;
using System.Diagnostics;
using System.Text.Json;

namespace AspireOllama.ApiService.Services.A2A;

public class A2AService(
    IHttpClientFactory httpClientFactory,
    ILogger<A2AService> logger) : IA2AService
{
    private static readonly Dictionary<string, string> AgentNames = new()
    {
        ["coordinator"] = "coordinator",
        ["planner"] = "planner",
        ["reviewer"] = "reviewer",
        ["research"] = "research",
        ["code"] = "code"
    };

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<AgentInfo>> GetAgentsAsync(CancellationToken ct = default)
    {
        var agents = new List<AgentInfo>();

        foreach (var (name, _) in AgentNames)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

                var client = httpClientFactory.CreateClient(name);
                var response = await client.GetAsync("/.well-known/agent.json", timeoutCts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var card = await response.Content.ReadFromJsonAsync<AgentCard>(_jsonOptions, ct);
                    agents.Add(new AgentInfo
                    {
                        Name = name,
                        Status = "connected",
                        Tools = card?.Skills?.Select(s => new AgentTool
                        {
                            Name = s.Id ?? "",
                            Description = s.Description ?? ""
                        }).ToList() ?? []
                    });
                }
                else
                {
                    agents.Add(new AgentInfo
                    {
                        Name = name,
                        Status = "disconnected",
                        Tools = []
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to connect to agent {Agent}", name);
                agents.Add(new AgentInfo
                {
                    Name = name,
                    Status = "error",
                    Tools = []
                });
            }
        }

        return agents;
    }

    public async Task<AgentCallResponse> CallAgentToolAsync(AgentCallRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // Build the message text that triggers the skill
            var messageText = BuildMessageForTool(request.ToolName, request.Arguments);

            var a2aRequest = new A2AMessageRequest
            {
                Message = new A2AMessage
                {
                    Role = "user",
                    Parts = [new A2APart { Text = messageText }]
                }
            };

            var client = httpClientFactory.CreateClient(request.AgentName);
            logger.LogInformation("Sending A2A message to {Agent} for skill {Skill}", request.AgentName, request.ToolName);

            var response = await client.PostAsJsonAsync("/a2a/message:send", a2aRequest, _jsonOptions, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<A2AMessageResponse>(_jsonOptions, ct);
            sw.Stop();

            // Extract result from task artifacts
            object? parsedResult = null;
            if (result?.Task?.Artifacts?.Count > 0)
            {
                var artifact = result.Task.Artifacts[0];
                if (artifact.Parts?.Count > 0)
                {
                    var part = artifact.Parts[0];
                    if (part.Data is not null)
                    {
                        parsedResult = part.Data;
                    }
                    else if (!string.IsNullOrEmpty(part.Text))
                    {
                        parsedResult = part.Text;
                    }
                }
            }

            // Fallback to task history if no artifacts
            if (parsedResult is null && result?.Task?.History?.Count > 0)
            {
                var lastAgentMessage = result.Task.History.LastOrDefault(h => h.Role == "agent");
                if (lastAgentMessage?.Parts?.Count > 0)
                {
                    parsedResult = lastAgentMessage.Parts[0].Text;
                }
            }

            return new AgentCallResponse
            {
                AgentName = request.AgentName,
                ToolName = request.ToolName,
                Success = result?.Task?.Status?.State == "Completed",
                Result = parsedResult,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "Error calling skill {Skill} on agent {Agent}", request.ToolName, request.AgentName);

            return new AgentCallResponse
            {
                AgentName = request.AgentName,
                ToolName = request.ToolName,
                Success = false,
                Error = ex.Message,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Delegates workflow orchestration to the Coordinator Agent.
    /// Parses the coordinator's named artifacts into individual interaction steps for the UI.
    /// </summary>
    public async Task<AgentWorkflowResponse> RunWorkflowAsync(AgentWorkflowRequest request, CancellationToken ct = default)
    {
        var totalSw = Stopwatch.StartNew();

        try
        {
            logger.LogInformation("Delegating workflow to Coordinator Agent: {Task}", request.Task);

            // Send to coordinator via A2A
            var messageText = request.Task;
            var a2aRequest = new A2AMessageRequest
            {
                Message = new A2AMessage
                {
                    Role = "user",
                    Parts = [new A2APart { Text = messageText }]
                }
            };

            var client = httpClientFactory.CreateClient("coordinator");
            var response = await client.PostAsJsonAsync("/a2a/message:send", a2aRequest, _jsonOptions, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<A2AMessageResponse>(_jsonOptions, ct);
            totalSw.Stop();

            // Parse the workflow_trace artifact for the UI
            var interactions = new List<AgentInteraction>();
            string? finalResult = null;

            logger.LogInformation("Coordinator returned {ArtifactCount} artifacts",
                result?.Task?.Artifacts?.Count ?? 0);

            if (result?.Task?.Artifacts is { Count: > 0 })
            {
                foreach (var a in result.Task.Artifacts)
                {
                    logger.LogInformation("Artifact: name={Name}, parts={Parts}",
                        a.Name ?? "(null)", a.Parts?.Count ?? 0);
                }

                // Find the workflow_trace artifact (structured JSON)
                var traceArtifact = result.Task.Artifacts.FirstOrDefault(a => a.Name == "workflow_trace");
                if (traceArtifact?.Parts?.FirstOrDefault()?.Text is { } traceJson)
                {
                    try
                    {
                        var steps = JsonSerializer.Deserialize<List<WorkflowTraceStep>>(traceJson, _jsonOptions);
                        if (steps is not null)
                        {
                            // Group steps by their dependencies to detect parallelism
                            var stepsByDeps = steps.GroupBy(s =>
                                s.DependsOn is { Count: > 0 } ? string.Join(",", s.DependsOn) : "none").ToList();

                            foreach (var step in steps)
                            {
                                // A step is parallel if another step has the same dependencies
                                var sameDeps = steps.Count(s =>
                                    s.Step != step.Step &&
                                    string.Join(",", s.DependsOn ?? []) == string.Join(",", step.DependsOn ?? []));

                                interactions.Add(new AgentInteraction
                                {
                                    Step = step.Step,
                                    AgentName = step.Agent ?? "unknown",
                                    ToolName = step.Action ?? "unknown",
                                    Arguments = new Dictionary<string, object?> { ["task"] = request.Task },
                                    DependsOn = step.DependsOn ?? [],
                                    Result = step.Result,
                                    ExecutionTimeMs = step.ElapsedMs,
                                    Status = step.Status ?? "completed",
                                    IsParallel = sameDeps > 0
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to parse workflow trace");
                    }
                }

                // Extract final response from the final_response artifact
                var finalArtifact = result.Task.Artifacts.FirstOrDefault(a => a.Name == "final_response");
                finalResult = finalArtifact?.Parts?.FirstOrDefault()?.Text;
            }

            // Fallback: if no trace was parsed, map each artifact as a step
            if (interactions.Count == 0 && result?.Task?.Artifacts is { Count: > 0 })
            {
                logger.LogWarning("No workflow_trace found, falling back to artifact-based interactions");
                var step = 1;
                foreach (var artifact in result.Task.Artifacts)
                {
                    var name = artifact.Name ?? $"step_{step}";
                    if (name == "workflow_trace") continue; // skip the trace itself

                    var text = artifact.Parts?.FirstOrDefault()?.Text ?? "";
                    interactions.Add(new AgentInteraction
                    {
                        Step = step++,
                        AgentName = name.Contains("plan") ? "planner"
                            : name.Contains("research") ? "research"
                            : name.Contains("code") ? "code"
                            : name.Contains("review") ? "reviewer"
                            : "coordinator",
                        ToolName = name,
                        Arguments = new Dictionary<string, object?> { ["task"] = request.Task },
                        Result = text.Length > 500 ? text[..500] + "..." : text,
                        ExecutionTimeMs = 0,
                        Status = "success"
                    });
                }
            }

            // Fallback chain: final_response artifact → last coordinator interaction → task history → status message
            if (string.IsNullOrWhiteSpace(finalResult) && interactions.Count > 0)
            {
                var lastCoord = interactions.LastOrDefault(i => i.AgentName == "coordinator");
                finalResult = lastCoord?.Result?.ToString();
            }
            if (string.IsNullOrWhiteSpace(finalResult) && result?.Task?.History?.Count > 0)
            {
                var lastAgent = result.Task.History.LastOrDefault(h => h.Role == "agent");
                finalResult = lastAgent?.Parts?.FirstOrDefault()?.Text;
            }
            finalResult ??= result?.Task?.Status?.Message ?? "Workflow completed";

            return new AgentWorkflowResponse
            {
                Task = request.Task,
                Interactions = interactions,
                FinalResult = finalResult,
                TotalExecutionTimeMs = totalSw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            totalSw.Stop();
            logger.LogError(ex, "Error delegating workflow to coordinator: {Task}", request.Task);

            return new AgentWorkflowResponse
            {
                Task = request.Task,
                Interactions = [],
                FinalResult = $"Workflow failed: {ex.Message}",
                TotalExecutionTimeMs = totalSw.ElapsedMilliseconds
            };
        }
    }

    private static string BuildMessageForTool(string toolName, Dictionary<string, object?>? arguments)
    {
        var args = arguments ?? new Dictionary<string, object?>();

        return toolName switch
        {
            "assess_complexity" => $"Assess the complexity of this task: {args.GetValueOrDefault("task")}",
            "suggest_agents" => $"Suggest which agents should handle this task: {args.GetValueOrDefault("task")}",
            "create_plan" => $"Create a plan for this task: {args.GetValueOrDefault("task")}",
            "orchestrate_task" => $"{args.GetValueOrDefault("task")}",
            "search_knowledge" => $"Search knowledge for: {args.GetValueOrDefault("query")}",
            "get_topic_details" => $"Get details about topic: {args.GetValueOrDefault("topic")}",
            "gather_context" => $"Gather context for: {args.GetValueOrDefault("topics")}",
            "suggest_topics" => $"Suggest related topics for: {args.GetValueOrDefault("query")}",
            "review_response" => $"Review this response: {args.GetValueOrDefault("response")}",
            "review_code" => $"Review this code: {args.GetValueOrDefault("code")}",
            "review_plan" => $"Review this plan: {args.GetValueOrDefault("plan")}",
            "provide_feedback" => $"Provide feedback on: {args.GetValueOrDefault("work")}",
            "execute_csharp" => $"Execute this C# code: {args.GetValueOrDefault("code")}",
            "generate_code" => $"Generate code for: {args.GetValueOrDefault("requirements")}",
            "analyze_code" => $"Analyze this code: {args.GetValueOrDefault("code")}",
            "generate_tests" => $"Generate tests for: {args.GetValueOrDefault("code")}",
            "refactor_code" => $"Refactor this code: {args.GetValueOrDefault("code")}",
            _ => $"{toolName}: {JsonSerializer.Serialize(args)}"
        };
    }
}

// A2A Protocol DTOs for ApiService
public class AgentCard
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Version { get; set; }
    public List<AgentSkill>? Skills { get; set; }
}

public class AgentSkill
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}

public class A2AMessageRequest
{
    public A2AMessage? Message { get; set; }
}

public class A2AMessage
{
    public string? Role { get; set; }
    public List<A2APart>? Parts { get; set; }
}

public class A2APart
{
    public string? Text { get; set; }
    public object? Data { get; set; }
}

public class A2AMessageResponse
{
    public A2ATaskInfo? Task { get; set; }
}

public class A2ATaskInfo
{
    public string? Id { get; set; }
    public A2ATaskStatus? Status { get; set; }
    public List<A2AArtifact>? Artifacts { get; set; }
    public List<A2AMessage>? History { get; set; }
}

public class A2ATaskStatus
{
    public string? State { get; set; }
    public string? Message { get; set; }
}

public class A2AArtifact
{
    public string? Name { get; set; }
    public List<A2APart>? Parts { get; set; }
}

public class WorkflowTraceStep
{
    public int Step { get; set; }
    public string? Agent { get; set; }
    public string? Action { get; set; }
    public string? Status { get; set; }
    public long ElapsedMs { get; set; }
    public string? Result { get; set; }
    public List<int>? DependsOn { get; set; }
}
