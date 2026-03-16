namespace AspireOllama.A2A.ReviewerAgent.Models.A2a;

/// <summary>
/// Response models used by the A2A server for JSON parsing from LLM
/// </summary>

public class ReviewResult
{
    public bool Approved { get; set; }
    public int Score { get; set; }
    public List<string>? Issues { get; set; }
    public string? Summary { get; set; }
    public List<string>? Improvements { get; set; }
}

public class CodeReviewResult
{
    public bool Approved { get; set; }
    public int IssueCount { get; set; }
    public List<CodeIssue>? Issues { get; set; }
    public string? Summary { get; set; }
    public int SecurityScore { get; set; }
    public int QualityScore { get; set; }
}

public class CodeIssue
{
    public string? Severity { get; set; }
    public string? Type { get; set; }
    public string? Description { get; set; }
    public string? Suggestion { get; set; }
}

public class AgentFeedbackResult
{
    public string? Verdict { get; set; }
    public int CompletionPercentage { get; set; }
    public List<string>? Strengths { get; set; }
    public List<string>? Weaknesses { get; set; }
    public string? DetailedFeedback { get; set; }
    public bool ShouldRetry { get; set; }
    public List<string>? NextSteps { get; set; }
}
