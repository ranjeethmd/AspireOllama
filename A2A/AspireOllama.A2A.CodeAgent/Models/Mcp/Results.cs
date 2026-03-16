namespace AspireOllama.A2A.CodeAgent.Models.Mcp;

/// <summary>
/// Result models returned by MCP tools
/// </summary>

public class ExecutionResult
{
    public bool Success { get; set; }
    public string? Output { get; set; }
    public string? Type { get; set; }
    public long ExecutionTimeMs { get; set; }
    public string? Error { get; set; }
    public string? Details { get; set; }
}

public class CodeGenResult
{
    public bool Success { get; set; }
    public string? Language { get; set; }
    public string? Code { get; set; }
    public string? Description { get; set; }
    public string? Explanation { get; set; }
    public string? Usage { get; set; }
    public string? Error { get; set; }
    public bool AiPowered { get; set; }
}

public class AnalysisResult
{
    public string Language { get; set; } = "";
    public int ComplexityScore { get; set; }
    public string ComplexityLevel { get; set; } = "";
    public List<string> DetectedPatterns { get; set; } = [];
    public List<string> PotentialIssues { get; set; } = [];
    public List<string> Suggestions { get; set; } = [];
    public string Summary { get; set; } = "";
    public bool AiPowered { get; set; }
}

public class TestGenResult
{
    public bool Success { get; set; }
    public string? Framework { get; set; }
    public string? TestCode { get; set; }
    public int TestCount { get; set; }
    public List<string> Scenarios { get; set; } = [];
    public string? Error { get; set; }
    public bool AiPowered { get; set; }
}

public class RefactorResult
{
    public bool Success { get; set; }
    public string OriginalCode { get; set; } = "";
    public string? RefactoredCode { get; set; }
    public string Goal { get; set; } = "";
    public List<string> Changes { get; set; } = [];
    public int ImprovementScore { get; set; }
    public string? Error { get; set; }
    public bool AiPowered { get; set; }
}
