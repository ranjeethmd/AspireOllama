namespace AspireOllama.A2A.PlannerAgent.Models.A2a;

/// <summary>
/// Response models used by the A2A server for JSON parsing from LLM
/// </summary>

public class PlanResult
{
    public string? Title { get; set; }
    public List<PlanStep>? Steps { get; set; }
    public string? EstimatedComplexity { get; set; }
    public string? Summary { get; set; }
}

public class PlanStep
{
    public int Step { get; set; }
    public string? Action { get; set; }
    public string? Agent { get; set; }
    public string? Details { get; set; }
}

public class ComplexityAssessment
{
    public string? ComplexityLevel { get; set; }
    public int Score { get; set; }
    public List<string>? Factors { get; set; }
    public bool NeedsPlanning { get; set; }
    public bool NeedsResearch { get; set; }
    public bool NeedsCode { get; set; }
    public bool NeedsReview { get; set; }
    public string? Reasoning { get; set; }
}

public class AgentSuggestions
{
    public List<AgentSuggestion>? Suggestions { get; set; }
    public string? Workflow { get; set; }
}

public class AgentSuggestion
{
    public string? Agent { get; set; }
    public string? Role { get; set; }
    public int Priority { get; set; }
    public string? Reason { get; set; }
}

public class WorkflowResult
{
    public string? Task { get; set; }
    public List<WorkflowStep>? Steps { get; set; }
    public bool Completed { get; set; }
}

public class WorkflowStep
{
    public int Step { get; set; }
    public string? Agent { get; set; }
    public string? Action { get; set; }
    public string? Result { get; set; }
}
