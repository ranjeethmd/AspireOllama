using Microsoft.Extensions.AI;

namespace AspireOllama.ApiService.Services.Mcp;

/// <summary>
/// Service for managing connections to MCP (Model Context Protocol) servers
/// and discovering their available tools.
/// </summary>
public interface IMcpService
{
    /// <summary>
    /// Gets all tools discovered from connected MCP servers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of AIFunction tools from MCP servers.</returns>
    Task<IReadOnlyList<AIFunction>> GetMcpToolsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes connections to all configured MCP servers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the names of all connected MCP servers.
    /// </summary>
    IReadOnlyList<string> GetConnectedServers();
}
