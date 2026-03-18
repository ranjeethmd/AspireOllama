using AspireOllama.ApiService.Services.Mcp;
using static AspireOllama.ServiceDefaults.Authentication.AuthRoles;
using FastEndpoints;

namespace AspireOllama.ApiService.Endpoints;

public class McpDebugResponse
{
    public int ConnectedServers { get; set; }
    public int ToolCount { get; set; }
    public List<string> Tools { get; set; } = [];
    public string? Error { get; set; }
}

public class McpDebugEndpoint(IMcpService mcpService) : EndpointWithoutRequest<McpDebugResponse>
{
    public override void Configure()
    {
        Get("/debug/mcp");
        Roles(ApiChatRead);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = new McpDebugResponse();

        try
        {
            response.ConnectedServers = mcpService.GetConnectedServers().Count;
            var tools = await mcpService.GetMcpToolsAsync(ct);
            response.ToolCount = tools.Count;
            response.Tools = tools.Select(t => t.Name).ToList();
        }
        catch (Exception ex)
        {
            response.Error = $"{ex.GetType().Name}: {ex.Message}";
        }

        await Send.OkAsync(response, ct);
    }
}
