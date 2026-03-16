namespace AspireOllama.A2A.PlannerAgent.Models.Mcp;

/// <summary>
/// Result models returned by MCP tools
/// </summary>

public class PlanResult
{
    public string Task { get; set; } = "";
    public List<PlanStep> Steps { get; set; } = [];
    public int TotalSteps { get; set; }
    public string Status { get; set; } = "";
    public string AiReasoning { get; set; } = "";
}

public class PlanStep
{
    public int StepNumber { get; set; }
    public string Action { get; set; } = "";
    public string Description { get; set; } = "";
    public string EstimatedComplexity { get; set; } = "";
    public string AssignedAgent { get; set; } = "";
}

public class ComplexityAssessment
{
    public string Task { get; set; } = "";
    public string Complexity { get; set; } = "";
    public bool NeedsPlanning { get; set; }
    public bool NeedsResearch { get; set; }
    public bool NeedsCodeGeneration { get; set; }
    public bool NeedsReview { get; set; }
    public string Reasoning { get; set; } = "";
    public string SuggestedApproach { get; set; } = "";
}

public class AgentSuggestion
{
    public string Task { get; set; } = "";
    public List<AgentRecommendation> RecommendedAgents { get; set; } = [];
    public List<string> ExecutionOrder { get; set; } = [];
    public string Workflow { get; set; } = "";
    public int EstimatedInteractions { get; set; }
}

public class AgentRecommendation
{
    public string AgentName { get; set; } = "";
    public string Role { get; set; } = "";
    public string Reason { get; set; } = "";
    public int Priority { get; set; }
}

public class OrchestrationResult
{
    public string Task { get; set; } = "";
    public bool IsComplete { get; set; }
    public string? NextAgent { get; set; }
    public string NextAction { get; set; } = "";
    public string Reasoning { get; set; } = "";
    public int Progress { get; set; }
    public string Summary { get; set; } = "";
}
