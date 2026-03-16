namespace AspireOllama.A2A.ResearchAgent.Models.Mcp;

/// <summary>
/// Result models returned by MCP tools
/// </summary>

public class SearchResult
{
    public string Query { get; set; } = "";
    public List<SearchMatch> Matches { get; set; } = [];
    public int TotalMatches { get; set; }
    public bool HasResults { get; set; }
    public string Synthesis { get; set; } = "";
    public bool AiPowered { get; set; }
}

public class SearchMatch
{
    public string Topic { get; set; } = "";
    public string Content { get; set; } = "";
    public double Relevance { get; set; }
}

public class TopicDetails
{
    public string Topic { get; set; } = "";
    public bool Found { get; set; }
    public string Overview { get; set; } = "";
    public string Content { get; set; } = "";
    public List<string> KeyConcepts { get; set; } = [];
    public List<string> UseCases { get; set; } = [];
    public bool AiPowered { get; set; }
}

public class ContextResult
{
    public List<string> RequestedTopics { get; set; } = [];
    public List<ContextItem> FoundItems { get; set; } = [];
    public string Synthesis { get; set; } = "";
    public double Confidence { get; set; }
    public bool AiPowered { get; set; }
}

public class ContextItem
{
    public string Topic { get; set; } = "";
    public string Content { get; set; } = "";
}

public class TopicSuggestions
{
    public string OriginalQuery { get; set; } = "";
    public List<SuggestedTopic> Suggestions { get; set; } = [];
    public string ResearchPath { get; set; } = "";
    public bool AiPowered { get; set; }
}

public class SuggestedTopic
{
    public string Topic { get; set; } = "";
    public double Relevance { get; set; }
    public string Reason { get; set; } = "";
}
