namespace AspireOllama.A2A.ReviewerAgent.Models.Mcp;

/// <summary>
/// Result models returned by MCP tools
/// </summary>

public class ReviewResult
{
    public bool Approved { get; set; }
    public int Score { get; set; }
    public List<string> Issues { get; set; } = [];
    public string Summary { get; set; } = "";
    public List<string> Improvements { get; set; } = [];
    public bool AiPowered { get; set; }
}

public class CodeReviewResult
{
    public bool Approved { get; set; }
    public int IssueCount { get; set; }
    public List<string> Issues { get; set; } = [];
    public string Summary { get; set; } = "";
    public int SecurityScore { get; set; }
    public int QualityScore { get; set; }
    public bool AiPowered { get; set; }
}

public class AgentFeedback
{
    public string AgentName { get; set; } = "";
    public string Verdict { get; set; } = "";
    public int CompletionPercentage { get; set; }
    public List<string> Strengths { get; set; } = [];
    public List<string> Weaknesses { get; set; } = [];
    public string DetailedFeedback { get; set; } = "";
    public bool ShouldRetry { get; set; }
    public bool AiPowered { get; set; }
}
