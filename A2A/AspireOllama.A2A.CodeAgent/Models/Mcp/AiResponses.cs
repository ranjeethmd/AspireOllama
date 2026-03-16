namespace AspireOllama.A2A.CodeAgent.Models.Mcp;

/// <summary>
/// AI response models for parsing LLM JSON outputs
/// </summary>

public class AICodeGenResponse
{
    public string? Code { get; set; }
    public string? Explanation { get; set; }
    public string? Usage { get; set; }
}

public class AIAnalysisResponse
{
    public int ComplexityScore { get; set; }
    public string? ComplexityLevel { get; set; }
    public List<string>? DetectedPatterns { get; set; }
    public List<string>? PotentialIssues { get; set; }
    public List<string>? Suggestions { get; set; }
    public string? Summary { get; set; }
}

public class AITestGenResponse
{
    public string? TestCode { get; set; }
    public int TestCount { get; set; }
    public List<string>? Scenarios { get; set; }
}

public class AIRefactorResponse
{
    public string? RefactoredCode { get; set; }
    public List<string>? Changes { get; set; }
    public int ImprovementScore { get; set; }
}
