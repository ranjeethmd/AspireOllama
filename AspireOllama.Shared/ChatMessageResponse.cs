namespace AspireOllama.Shared;

public class ChatMessageResponse
{
    public string SessionId { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// List of tool calls made during this response.
    /// Empty if no tools were used.
    /// </summary>
    public List<ToolCall> ToolCalls { get; set; } = new();

    /// <summary>
    /// Indicates whether any tools were used for this response.
    /// </summary>
    public bool UsedTools => ToolCalls.Count > 0;

    /// <summary>
    /// Usage information including tokens, timing, and model details.
    /// </summary>
    public UsageInfo Usage { get; set; } = new();
}
