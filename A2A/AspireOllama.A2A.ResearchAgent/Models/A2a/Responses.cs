namespace AspireOllama.A2A.ResearchAgent.Models.A2a;

/// <summary>
/// Response models used by the A2A server for JSON parsing from LLM
/// </summary>

public class SearchResult
{
    public List<SearchMatch>? Matches { get; set; }
    public string? Synthesis { get; set; }
    public double Confidence { get; set; }
}

public class SearchMatch
{
    public string? Topic { get; set; }
    public string? Content { get; set; }
    public double Relevance { get; set; }
}

public class TopicDetailsResult
{
    public string? Topic { get; set; }
    public string? Overview { get; set; }
    public string? Details { get; set; }
    public List<string>? KeyConcepts { get; set; }
    public List<string>? UseCases { get; set; }
    public List<string>? RelatedTopics { get; set; }
}

public class ContextResult
{
    public List<string>? Topics { get; set; }
    public List<ContextItem>? FoundItems { get; set; }
    public string? Synthesis { get; set; }
    public double Confidence { get; set; }
    public List<string>? Gaps { get; set; }
}

public class ContextItem
{
    public string? Topic { get; set; }
    public string? Content { get; set; }
}

public class SuggestionsResult
{
    public List<TopicSuggestion>? Suggestions { get; set; }
    public string? ResearchPath { get; set; }
    public List<string>? AdditionalSuggestions { get; set; }
}

public class TopicSuggestion
{
    public string? Topic { get; set; }
    public double Relevance { get; set; }
    public string? Reason { get; set; }
}
