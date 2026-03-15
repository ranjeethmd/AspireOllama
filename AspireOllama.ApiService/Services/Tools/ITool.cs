namespace AspireOllama.ApiService.Services.Tools;

/// <summary>
/// Marker interface for tool implementations.
/// Tools are functions that the AI model can call to perform actions.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Unique name of the tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description of what this tool does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Whether this tool is currently enabled and available for use.
    /// </summary>
    bool IsEnabled { get; }
}
