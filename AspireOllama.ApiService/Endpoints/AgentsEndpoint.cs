using AspireOllama.ApiService.Services.A2A;
using AspireOllama.Shared;
using FastEndpoints;
using static AspireOllama.ServiceDefaults.Authentication.AuthRoles;

namespace AspireOllama.ApiService.Endpoints;

// Debug endpoint to test raw HTTP connectivity to agents
public class TestAgentConnectivityEndpoint(IHttpClientFactory httpClientFactory, ILogger<TestAgentConnectivityEndpoint> logger)
    : EndpointWithoutRequest<Dictionary<string, object>>
{
    public override void Configure()
    {
        Get("/agents/test");
        Roles(ApiChatRead);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var results = new Dictionary<string, object>();
        var agents = new[] { "planner", "reviewer", "research", "code" };

        foreach (var agent in agents)
        {
            try
            {
                var client = httpClientFactory.CreateClient(agent);
                logger.LogInformation("Testing {Agent} at {BaseAddress}", agent, client.BaseAddress);

                // Test root endpoint
                var response = await client.GetAsync("/", ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                // Test health endpoint
                var healthResponse = await client.GetAsync("/health", ct);
                var healthContent = await healthResponse.Content.ReadAsStringAsync(ct);

                results[agent] = new
                {
                    baseAddress = client.BaseAddress?.ToString(),
                    rootStatus = (int)response.StatusCode,
                    rootContent = content,
                    healthStatus = (int)healthResponse.StatusCode,
                    healthContent = healthContent
                };
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to connect to {Agent}", agent);
                results[agent] = new
                {
                    error = ex.Message,
                    innerError = ex.InnerException?.Message
                };
            }
        }

        await Send.OkAsync(results, ct);
    }
}

public class GetAgentsEndpoint(IA2AService a2aService) : EndpointWithoutRequest<List<AgentInfo>>
{
    public override void Configure()
    {
        Get("/agents");
        Roles(ApiChatRead);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var agents = await a2aService.GetAgentsAsync(ct);
        await Send.OkAsync(agents, ct);
    }
}

public class CallAgentEndpoint(IA2AService a2aService) : Endpoint<AgentCallRequest, AgentCallResponse>
{
    public override void Configure()
    {
        Post("/agents/call");
        Roles(ApiChatWrite);
    }

    public override async Task HandleAsync(AgentCallRequest req, CancellationToken ct)
    {
        var result = await a2aService.CallAgentToolAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}

public class RunWorkflowEndpoint(IA2AService a2aService) : Endpoint<AgentWorkflowRequest, AgentWorkflowResponse>
{
    public override void Configure()
    {
        Post("/agents/workflow");
        Roles(ApiChatWrite);
    }

    public override async Task HandleAsync(AgentWorkflowRequest req, CancellationToken ct)
    {
        var result = await a2aService.RunWorkflowAsync(req, ct);
        await Send.OkAsync(result, ct);
    }
}
