namespace AspireOllama.Shared;

/// <summary>
/// Represents usage information for an AI chat response.
/// Tracks token usage, timing, and model information.
/// </summary>
public class UsageInfo
{
    /// <summary>
    /// Number of tokens in the input prompt.
    /// </summary>
    public long PromptTokens { get; set; }

    /// <summary>
    /// Number of tokens in the generated response.
    /// </summary>
    public long CompletionTokens { get; set; }

    /// <summary>
    /// Total tokens used (prompt + completion).
    /// </summary>
    public long TotalTokens => PromptTokens + CompletionTokens;

    /// <summary>
    /// Name of the AI model used for this response.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Total time taken for the response in milliseconds.
    /// </summary>
    public long ResponseTimeMs { get; set; }

    /// <summary>
    /// Time taken to process the prompt in milliseconds.
    /// </summary>
    public long PromptEvalTimeMs { get; set; }

    /// <summary>
    /// Time taken to generate the response in milliseconds.
    /// </summary>
    public long EvalTimeMs { get; set; }

    /// <summary>
    /// Tokens generated per second.
    /// </summary>
    public double TokensPerSecond => EvalTimeMs > 0 ? (CompletionTokens * 1000.0 / EvalTimeMs) : 0;

    /// <summary>
    /// Number of images processed in the request.
    /// </summary>
    public int ImagesProcessed { get; set; }

    /// <summary>
    /// Number of documents processed in the request.
    /// </summary>
    public int DocumentsProcessed { get; set; }

    /// <summary>
    /// Number of tool/skill calls made during the response.
    /// </summary>
    public int SkillCallsCount { get; set; }

    /// <summary>
    /// Total time spent executing skills in milliseconds.
    /// </summary>
    public long TotalSkillExecutionTimeMs { get; set; }
}
