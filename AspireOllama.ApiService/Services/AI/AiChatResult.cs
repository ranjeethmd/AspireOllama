using AspireOllama.Shared;

namespace AspireOllama.ApiService.Services.AI;

/// <summary>
/// Result from an AI chat operation, including response text and any tool calls made.
/// </summary>
public class AiChatResult
{
    /// <summary>
    /// The AI's text response.
    /// </summary>
    public string Response { get; set; } = string.Empty;

    /// <summary>
    /// List of tool calls made during the response generation.
    /// </summary>
    public List<ToolCall> ToolCalls { get; set; } = new();

    /// <summary>
    /// Usage information including tokens, timing, and model details.
    /// </summary>
    public UsageInfo Usage { get; set; } = new();
}
