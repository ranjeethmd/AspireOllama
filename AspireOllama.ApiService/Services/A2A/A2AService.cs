using System.Diagnostics;
using System.Text.Json;
using AspireOllama.Shared;
using ModelContextProtocol.Client;

namespace AspireOllama.ApiService.Services.A2A;

public class A2AService(
    IHttpClientFactory httpClientFactory,
    ILogger<A2AService> logger) : IA2AService, IAsyncDisposable
{
    private static readonly Dictionary<string, string> AgentEndpoints = new()
    {
        ["planner"] = "planner-agent",
        ["reviewer"] = "reviewer-agent",
        ["research"] = "research-agent",
        ["code"] = "code-agent"
    };

    private readonly Dictionary<string, McpClient?> _clients = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task<List<AgentInfo>> GetAgentsAsync(CancellationToken ct = default)
    {
        var agents = new List<AgentInfo>();

        foreach (var (name, serviceName) in AgentEndpoints)
        {
            try
            {
                var client = await GetOrCreateClientAsync(name, ct);
                if (client is not null)
                {
                    var tools = await client.ListToolsAsync();
                    agents.Add(new AgentInfo
                    {
                        Name = name,
                        Status = "connected",
                        Tools = tools.Select(t => new AgentTool
                        {
                            Name = t.Name,
                            Description = t.Description ?? ""
                        }).ToList()
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
            var client = await GetOrCreateClientAsync(request.AgentName, ct);
            if (client == null)
            {
                return new AgentCallResponse
                {
                    AgentName = request.AgentName,
                    ToolName = request.ToolName,
                    Success = false,
                    Error = $"Agent '{request.AgentName}' not available"
                };
            }

            var result = await client.CallToolAsync(
                request.ToolName,
                request.Arguments,
                cancellationToken: ct);

            sw.Stop();

            // Extract text content from result - use ToString for ContentBlock
            var textContent = result.Content
                .Select(c => c.ToString())
                .FirstOrDefault();

            object? parsedResult = textContent;

            // Try to parse as JSON
            if (!string.IsNullOrEmpty(textContent))
            {
                try
                {
                    parsedResult = JsonSerializer.Deserialize<JsonElement>(textContent);
                }
                catch
                {
                    // Keep as string if not valid JSON
                }
            }

            return new AgentCallResponse
            {
                AgentName = request.AgentName,
                ToolName = request.ToolName,
                Success = result.IsError != true,
                Result = parsedResult,
                ExecutionTimeMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "Error calling tool {Tool} on agent {Agent}", request.ToolName, request.AgentName);

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
                Arguments = new Dictionary<string, object?> { ["planJson"] = planJson }
            }, ct);

            interactions.Add(new AgentInteraction
            {
                Step = step++,
                AgentName = "reviewer",
                ToolName = "review_plan",
                Arguments = new Dictionary<string, object?> { ["planJson"] = "(plan)" },
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

    private async Task<McpClient?> GetOrCreateClientAsync(string agentName, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_clients.TryGetValue(agentName, out var existingClient) && existingClient is not null)
            {
                return existingClient;
            }

            if (!AgentEndpoints.TryGetValue(agentName, out var serviceName))
            {
                return null;
            }

            try
            {
                var httpClient = httpClientFactory.CreateClient(agentName);

                // The HttpClient base address is configured by Aspire
                // MCP endpoint is at /mcp path
                var baseAddress = httpClient.BaseAddress ?? new Uri($"http://{serviceName}");
                var mcpEndpoint = new Uri(baseAddress, "/mcp");

                logger.LogInformation("Connecting to agent {Agent} at {Endpoint}", agentName, mcpEndpoint);

                var transportOptions = new HttpClientTransportOptions
                {
                    Endpoint = mcpEndpoint,
                    Name = agentName
                };

                var transport = new HttpClientTransport(transportOptions, httpClient);
                var client = await McpClient.CreateAsync(transport, cancellationToken: ct);

                _clients[agentName] = client;
                logger.LogInformation("Connected to agent {Agent}", agentName);

                return client;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to create MCP client for agent {Agent}", agentName);
                return null;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var client in _clients.Values)
        {
            if (client is not null)
            {
                try
                {
                    await client.DisposeAsync();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error disposing MCP client");
                }
            }
        }

        _clients.Clear();
        _lock.Dispose();
    }
}
