using Microsoft.Extensions.AI;

namespace AspireOllama.ApiService.Services.Tools;

/// <summary>
/// Registry for collecting and managing AI tools.
/// Provides access to all enabled tools from built-in implementations and MCP servers.
/// </summary>
public interface IToolRegistry
{
    /// <summary>
    /// Gets all registered AIFunction tools based on current configuration.
    /// </summary>
    /// <returns>List of enabled AIFunction tools.</returns>
    IReadOnlyList<AIFunction> GetEnabledTools();

    /// <summary>
    /// Registers a new tool at runtime.
    /// </summary>
    /// <param name="tool">The AIFunction tool to register.</param>
    void RegisterTool(AIFunction tool);
}
