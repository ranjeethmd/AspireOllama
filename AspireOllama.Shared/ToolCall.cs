namespace AspireOllama.Shared;

/// <summary>
/// Represents a tool call made by the AI model during a chat response.
/// </summary>
public class ToolCall
{
    /// <summary>
    /// Unique identifier for this tool call.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Name of the tool that was called.
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Arguments passed to the tool.
    /// </summary>
    public Dictionary<string, object?> Arguments { get; set; } = new();

    /// <summary>
    /// Result returned by the tool execution.
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// Current execution status of the tool call.
    /// </summary>
    public ToolCallStatus Status { get; set; } = ToolCallStatus.Pending;

    /// <summary>
    /// Time taken to execute the tool in milliseconds.
    /// </summary>
    public long ExecutionTimeMs { get; set; }
}

/// <summary>
/// Status of a tool call execution.
/// </summary>
public enum ToolCallStatus
{
    /// <summary>Tool call is queued but not yet started.</summary>
    Pending,

    /// <summary>Tool is currently executing.</summary>
    Executing,

    /// <summary>Tool executed successfully.</summary>
    Completed,

    /// <summary>Tool execution failed.</summary>
    Failed
}
