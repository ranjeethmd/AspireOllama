namespace AspireOllama.A2A.PlannerAgent.Models.Mcp;

/// <summary>
/// AI response models for parsing LLM JSON outputs
/// </summary>

public class AIPlanResponse
{
    public List<AIPlanStep>? Steps { get; set; }
    public string? Reasoning { get; set; }
}

public class AIPlanStep
{
    public int StepNumber { get; set; }
    public string? Action { get; set; }
    public string? Description { get; set; }
    public string? Complexity { get; set; }
    public string? AssignedAgent { get; set; }
}

public class AIComplexityResponse
{
    public string? Complexity { get; set; }
    public bool NeedsPlanning { get; set; }
    public bool NeedsResearch { get; set; }
    public bool NeedsCodeGeneration { get; set; }
    public bool NeedsReview { get; set; }
    public string? Reasoning { get; set; }
    public string? SuggestedApproach { get; set; }
}

public class AIAgentSuggestionResponse
{
    public List<AIAgentRec>? Agents { get; set; }
    public string? Workflow { get; set; }
    public int EstimatedInteractions { get; set; }
}

public class AIAgentRec
{
    public string? Name { get; set; }
    public string? Role { get; set; }
    public string? Reason { get; set; }
    public int Priority { get; set; }
}

public class AIOrchestrationResponse
{
    public bool IsComplete { get; set; }
    public string? NextAgent { get; set; }
    public string? NextAction { get; set; }
    public string? Reasoning { get; set; }
    public int Progress { get; set; }
    public string? Summary { get; set; }
}
