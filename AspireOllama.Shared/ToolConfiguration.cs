namespace AspireOllama.Shared;

/// <summary>
/// Configuration for built-in tool calling.
/// MCP servers are discovered via Aspire service discovery.
/// </summary>
public class ToolConfiguration
{
    /// <summary>
    /// Enable the calculator tool for math expressions.
    /// </summary>
    public bool EnableCalculator { get; set; } = true;

    /// <summary>
    /// Enable web search tool. Requires API key configuration.
    /// </summary>
    public bool EnableWebSearch { get; set; } = false;

    /// <summary>
    /// Enable C# code execution tool. Use with caution.
    /// </summary>
    public bool EnableCodeExecution { get; set; } = false;

    /// <summary>
    /// Enable file operations tool within sandbox directory.
    /// </summary>
    public bool EnableFileOperations { get; set; } = false;

    /// <summary>
    /// Configuration for sandbox directory used by file operations.
    /// </summary>
    public string SandboxPath { get; set; } = "./sandbox";
}
