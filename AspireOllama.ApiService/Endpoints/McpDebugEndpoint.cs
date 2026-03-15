using AspireOllama.ApiService.Services.Mcp;
using FastEndpoints;

namespace AspireOllama.ApiService.Endpoints;

public class McpDebugResponse
{
    public string? ConnectionString { get; set; }
    public string? McpEndpoint { get; set; }
    public string? HttpCheck { get; set; }
    public int ConnectedServers { get; set; }
    public int ToolCount { get; set; }
    public List<string> Tools { get; set; } = new();
    public string? Error { get; set; }
}

public class McpDebugEndpoint : EndpointWithoutRequest<McpDebugResponse>
{
    private readonly IMcpService _mcpService;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public McpDebugEndpoint(IMcpService mcpService, IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _mcpService = mcpService;
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public override void Configure()
    {
        Get("/debug/mcp");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = new McpDebugResponse();

        try
        {
            response.ConnectionString = _config.GetConnectionString("mcpserver");
            response.McpEndpoint = response.ConnectionString?.TrimEnd('/') + "/mcp";
            response.ConnectedServers = _mcpService.GetConnectedServers().Count;

            // Test direct HTTP connectivity to MCP server
            if (!string.IsNullOrEmpty(response.ConnectionString))
            {
                try
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    var httpResponse = await httpClient.GetAsync(response.ConnectionString, ct);
                    response.HttpCheck = $"GET {response.ConnectionString} => {(int)httpResponse.StatusCode} {httpResponse.StatusCode}";
                }
                catch (Exception httpEx)
                {
                    response.HttpCheck = $"HTTP Error: {httpEx.Message}";
                }
            }

            var tools = await _mcpService.GetMcpToolsAsync(ct);
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
