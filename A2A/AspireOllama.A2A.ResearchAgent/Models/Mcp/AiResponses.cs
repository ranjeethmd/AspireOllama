namespace AspireOllama.A2A.ResearchAgent.Models.Mcp;

/// <summary>
/// AI response models for parsing LLM JSON outputs
/// </summary>

public class AISearchResponse
{
    public List<AISearchMatch>? Matches { get; set; }
    public string? Synthesis { get; set; }
}

public class AISearchMatch
{
    public string? Topic { get; set; }
    public string? Content { get; set; }
    public double Relevance { get; set; }
}

public class AITopicResponse
{
    public string? Overview { get; set; }
    public string? Details { get; set; }
    public List<string>? KeyConcepts { get; set; }
    public List<string>? UseCases { get; set; }
}

public class AIContextResponse
{
    public List<AIContextItem>? FoundItems { get; set; }
    public string? Synthesis { get; set; }
    public double Confidence { get; set; }
}

public class AIContextItem
{
    public string? Topic { get; set; }
    public string? Content { get; set; }
}

public class AISuggestionsResponse
{
    public List<AISuggestion>? Suggestions { get; set; }
    public string? ResearchPath { get; set; }
}

public class AISuggestion
{
    public string? Topic { get; set; }
    public double Relevance { get; set; }
    public string? Reason { get; set; }
}
