namespace AspireOllama.A2A.CodeAgent.Models.A2a;

/// <summary>
/// Response models used by the A2A server for JSON parsing from LLM
/// </summary>

public class ExecutionResult
{
    public bool Success { get; set; }
    public string? Code { get; set; }
    public string? Output { get; set; }
    public string? Type { get; set; }
    public string? Error { get; set; }
    public string? Details { get; set; }
    public long ExecutionTimeMs { get; set; }
}

public class CodeGenResult
{
    public string? Code { get; set; }
    public string? Language { get; set; }
    public string? Explanation { get; set; }
    public string? Usage { get; set; }
}

public class AnalysisResult
{
    public int ComplexityScore { get; set; }
    public string? ComplexityLevel { get; set; }
    public List<string>? DetectedPatterns { get; set; }
    public List<string>? PotentialIssues { get; set; }
    public List<string>? Suggestions { get; set; }
    public string? Summary { get; set; }
}

public class TestGenResult
{
    public string? TestCode { get; set; }
    public string? Framework { get; set; }
    public int TestCount { get; set; }
    public List<string>? Scenarios { get; set; }
}

public class RefactorResult
{
    public string? RefactoredCode { get; set; }
    public List<string>? Changes { get; set; }
    public int ImprovementScore { get; set; }
    public string? Explanation { get; set; }
}
