namespace AspireOllama.A2A.ReviewerAgent.Models.Mcp;

/// <summary>
/// AI response models for parsing LLM JSON outputs
/// </summary>

public class AIReviewResponse
{
    public bool Approved { get; set; }
    public int Score { get; set; }
    public List<string>? Issues { get; set; }
    public string? Summary { get; set; }
    public List<string>? Improvements { get; set; }
}

public class AICodeReviewResponse
{
    public bool Approved { get; set; }
    public int IssueCount { get; set; }
    public List<string>? Issues { get; set; }
    public string? Summary { get; set; }
    public int SecurityScore { get; set; }
    public int QualityScore { get; set; }
}

public class AIFeedbackResponse
{
    public string? Verdict { get; set; }
    public int CompletionPercentage { get; set; }
    public List<string>? Strengths { get; set; }
    public List<string>? Weaknesses { get; set; }
    public string? DetailedFeedback { get; set; }
    public bool ShouldRetry { get; set; }
}
