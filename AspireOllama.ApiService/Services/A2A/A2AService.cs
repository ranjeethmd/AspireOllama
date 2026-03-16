using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using AspireOllama.Shared;

namespace AspireOllama.ApiService.Services.A2A;

public class A2AService(
    IHttpClientFactory httpClientFactory,
    ILogger<A2AService> logger) : IA2AService
{
    private static readonly Dictionary<string, string> AgentNames = new()
    {
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
                var client = httpClientFactory.CreateClient(name);
                var response = await client.GetAsync("/.well-known/agent.json", ct);

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

    public async Task<AgentWorkflowResponse> RunWorkflowAsync(AgentWorkflowRequest request, CancellationToken ct = default)
    {
        var totalSw = Stopwatch.StartNew();
        var interactions = new List<AgentInteraction>();
        var step = 1;

        try
        {
            // Step 1: Assess complexity with Planner
            var complexityResult = await CallAgentToolAsync(new AgentCallRequest
            {
                AgentName = "planner",
                ToolName = "assess_complexity",
                Arguments = new Dictionary<string, object?> { ["task"] = request.Task }
            }, ct);

            interactions.Add(new AgentInteraction
            {
                Step = step++,
                AgentName = "planner",
                ToolName = "assess_complexity",
                Arguments = new Dictionary<string, object?> { ["task"] = request.Task },
                Result = complexityResult.Result,
                ExecutionTimeMs = complexityResult.ExecutionTimeMs,
                Status = complexityResult.Success ? "success" : "failed"
            });

            // Step 2: Suggest agents
            var suggestResult = await CallAgentToolAsync(new AgentCallRequest
            {
                AgentName = "planner",
                ToolName = "suggest_agents",
                Arguments = new Dictionary<string, object?> { ["task"] = request.Task }
            }, ct);

            interactions.Add(new AgentInteraction
            {
                Step = step++,
                AgentName = "planner",
                ToolName = "suggest_agents",
                Arguments = new Dictionary<string, object?> { ["task"] = request.Task },
                Result = suggestResult.Result,
                ExecutionTimeMs = suggestResult.ExecutionTimeMs,
                Status = suggestResult.Success ? "success" : "failed"
            });

            // Step 3: Create plan
            var planResult = await CallAgentToolAsync(new AgentCallRequest
            {
                AgentName = "planner",
                ToolName = "create_plan",
                Arguments = new Dictionary<string, object?>
                {
                    ["task"] = request.Task,
                    ["maxSteps"] = 5
                }
            }, ct);

            interactions.Add(new AgentInteraction
            {
                Step = step++,
                AgentName = "planner",
                ToolName = "create_plan",
                Arguments = new Dictionary<string, object?> { ["task"] = request.Task },
                Result = planResult.Result,
                ExecutionTimeMs = planResult.ExecutionTimeMs,
                Status = planResult.Success ? "success" : "failed"
            });

            // Step 4: Research context
            var researchResult = await CallAgentToolAsync(new AgentCallRequest
            {
                AgentName = "research",
                ToolName = "search_knowledge",
                Arguments = new Dictionary<string, object?> { ["query"] = request.Task }
            }, ct);

            interactions.Add(new AgentInteraction
            {
                Step = step++,
                AgentName = "research",
                ToolName = "search_knowledge",
                Arguments = new Dictionary<string, object?> { ["query"] = request.Task },
                Result = researchResult.Result,
                ExecutionTimeMs = researchResult.ExecutionTimeMs,
                Status = researchResult.Success ? "success" : "failed"
            });

            // Step 5: Review the plan
            var planJson = JsonSerializer.Serialize(planResult.Result);
            var reviewResult = await CallAgentToolAsync(new AgentCallRequest
            {
                AgentName = "reviewer",
                ToolName = "review_plan",
                Arguments = new Dictionary<string, object?> { ["plan"] = planJson }
            }, ct);

            interactions.Add(new AgentInteraction
            {
                Step = step++,
                AgentName = "reviewer",
                ToolName = "review_plan",
                Arguments = new Dictionary<string, object?> { ["plan"] = "(plan)" },
                Result = reviewResult.Result,
                ExecutionTimeMs = reviewResult.ExecutionTimeMs,
                Status = reviewResult.Success ? "success" : "failed"
            });

            totalSw.Stop();

            return new AgentWorkflowResponse
            {
                Task = request.Task,
                Interactions = interactions,
                FinalResult = $"Workflow completed with {interactions.Count} agent interactions",
                TotalExecutionTimeMs = totalSw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            totalSw.Stop();
            logger.LogError(ex, "Error running workflow for task: {Task}", request.Task);

            return new AgentWorkflowResponse
            {
                Task = request.Task,
                Interactions = interactions,
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
            "orchestrate_workflow" => $"Orchestrate a workflow for: {args.GetValueOrDefault("task")}",
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
    public List<A2APart>? Parts { get; set; }
}
