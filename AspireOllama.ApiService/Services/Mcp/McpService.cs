using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace AspireOllama.ApiService.Services.Mcp;

/// <summary>
/// Service for managing MCP server connections and tool discovery.
/// Connects to MCP servers via HTTP (managed by Aspire service discovery).
/// </summary>
public class McpService : IMcpService, IAsyncDisposable
{
    private readonly List<McpClient> _mcpClients = new();
    private readonly List<AIFunction> _mcpTools = new();
    private readonly List<string> _connectedServers = new();
    private readonly HttpClient _mcpServerClient;
    private readonly ILogger<McpService> _logger;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public McpService(
        IHttpClientFactory httpClientFactory,
        ILogger<McpService> logger)
    {
        // Use named HttpClient configured by Aspire service discovery
        _mcpServerClient = httpClientFactory.CreateClient("mcpserver");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;

            _logger.LogInformation("Initializing MCP service...");

            // Connect using the named HttpClient configured by Aspire service discovery
            await ConnectToServerAsync("mcpserver", cancellationToken);

            _initialized = true;
            _logger.LogInformation("MCP service initialized with {ToolCount} tools from {ServerCount} servers",
                _mcpTools.Count, _connectedServers.Count);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task ConnectToServerAsync(string name, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        const int retryDelayMs = 2000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Connecting to MCP server: {Name} (attempt {Attempt}/{MaxRetries})",
                    name, attempt, maxRetries);

                // The HttpClient is configured by Aspire with the base address
                // MCP endpoint is at /mcp path
                var mcpEndpoint = new Uri(_mcpServerClient.BaseAddress!, "/mcp");

                _logger.LogInformation("MCP endpoint: {Endpoint}", mcpEndpoint);

                // Create HTTP transport options
                var transportOptions = new HttpClientTransportOptions
                {
                    Endpoint = mcpEndpoint,
                    Name = name
                };

                // Create HTTP transport for MCP server using the Aspire-configured client
                var transport = new HttpClientTransport(transportOptions, _mcpServerClient);

                // Create MCP client
                var mcpClient = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);

                _mcpClients.Add(mcpClient);
                _connectedServers.Add(name);

                // Discover tools from this server
                var tools = await mcpClient.ListToolsAsync();
                foreach (var tool in tools)
                {
                    _mcpTools.Add(tool);
                    _logger.LogInformation("  Discovered MCP tool: {ToolName} from {ServerName}",
                        tool.Name, name);
                }

                _logger.LogInformation("Connected to MCP server: {Name} with {ToolCount} tools",
                    name, tools.Count);

                return; // Success - exit retry loop
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to MCP server: {Name} (attempt {Attempt}/{MaxRetries}). Error: {Error}",
                    name, attempt, maxRetries, ex.Message);

                if (ex.InnerException is not null)
                {
                    _logger.LogWarning("Inner exception: {InnerError}", ex.InnerException.Message);
                }

                if (attempt < maxRetries)
                {
                    _logger.LogInformation("Retrying in {Delay}ms...", retryDelayMs);
                    await Task.Delay(retryDelayMs, cancellationToken);
                }
                else
                {
                    _logger.LogError("All {MaxRetries} attempts failed for MCP server: {Name}", maxRetries, name);
                }
            }
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AIFunction>> GetMcpToolsAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            await InitializeAsync(cancellationToken);
        }

        return _mcpTools.AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetConnectedServers()
    {
        return _connectedServers.AsReadOnly();
    }

    /// <summary>
    /// Disposes all MCP client connections.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var client in _mcpClients)
        {
            try
            {
                await client.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing MCP client");
            }
        }

        _mcpClients.Clear();
        _mcpTools.Clear();
        _connectedServers.Clear();
        _initLock.Dispose();
    }
}
