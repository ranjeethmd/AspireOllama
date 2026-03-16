using AspireOllama.A2A.ResearchAgent.Models.Mcp;
using ModelContextProtocol.Server;
using OllamaSharp;
using OllamaSharp.AsyncEnumerableExtensions;
using System.ComponentModel;
using System.Text.Json;

namespace AspireOllama.A2A.ResearchAgent.Tools;

[McpServerToolType]
public static class ResearchTools
{
    private static IOllamaApiClient? _ollamaClient;

    private static readonly Dictionary<string, string> KnowledgeBase = new()
    {
        ["dotnet"] = ".NET is a free, cross-platform developer platform for building many types of applications.",
        ["aspire"] = ".NET Aspire is an opinionated, cloud ready stack for building observable, production ready, distributed applications.",
        ["ollama"] = "Ollama is a tool for running large language models locally. It supports models like llama, mistral, llava.",
        ["mcp"] = "Model Context Protocol (MCP) is a standard for connecting AI models to external tools and data sources.",
        ["blazor"] = "Blazor is a framework for building interactive web UI with .NET instead of JavaScript.",
        ["a2a"] = "Agent-to-Agent (A2A) protocol enables AI agents to communicate and collaborate with each other."
    };

    public static void Initialize(IOllamaApiClient client) => _ollamaClient = client;

    [McpServerTool, Description("Uses AI to search knowledge base and synthesize relevant information")]
    public static async Task<SearchResult> search_knowledge(
        [Description("The topic or keywords to search for")] string query)
    {
        var client = GetClient();
        var relevantEntries = KnowledgeBase.Where(kv =>
            query.ToLower().Contains(kv.Key) || kv.Key.Contains(query.ToLower()) ||
            kv.Value.ToLower().Contains(query.ToLower())).ToDictionary(kv => kv.Key, kv => kv.Value);

        var prompt = "You are a research assistant. Search for: \"" + query + "\"\n\n" +
            "Available knowledge:\n" + string.Join("\n", relevantEntries.Select(e => "- " + e.Key + ": " + e.Value)) + "\n\n" +
            "Respond in JSON: {\"matches\": [{\"topic\": \"name\", \"content\": \"info\", \"relevance\": 0.9}], \"synthesis\": \"comprehensive answer\"}";

        try
        {
            var result = await client.GenerateAsync(prompt).StreamToEndAsync();
            var content = result?.Response ?? "";
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<AISearchResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed is not null)
                {
                    return new SearchResult
                    {
                        Query = query,
                        Matches = parsed.Matches?.Select(m => new SearchMatch { Topic = m.Topic ?? "", Content = m.Content ?? "", Relevance = m.Relevance }).ToList() ?? [],
                        TotalMatches = parsed.Matches?.Count ?? 0,
                        HasResults = parsed.Matches?.Count > 0,
                        Synthesis = parsed.Synthesis ?? "",
                        AiPowered = true
                    };
                }
            }
            return new SearchResult { Query = query, Synthesis = content, TotalMatches = relevantEntries.Count, HasResults = relevantEntries.Count > 0, AiPowered = false };
        }
        catch (Exception ex)
        {
            return new SearchResult { Query = query, Synthesis = "Error: " + ex.Message, AiPowered = false };
        }
    }

    [McpServerTool, Description("Uses AI to get detailed information about a topic")]
    public static async Task<TopicDetails> get_topic_details(
        [Description("The topic name to look up")] string topic)
    {
        var client = GetClient();
        var hasContent = KnowledgeBase.TryGetValue(topic.ToLower(), out var baseContent);

        var prompt = "Provide comprehensive details about: \"" + topic + "\"\n\n" +
            "Base knowledge: " + (baseContent ?? "Use your training data.") + "\n\n" +
            "Respond in JSON: {\"topic\": \"name\", \"found\": true, \"overview\": \"summary\", \"details\": \"explanation\", \"keyConcepts\": [], \"useCases\": []}";

        try
        {
            var result = await client.GenerateAsync(prompt).StreamToEndAsync();
            var content = result?.Response ?? "";
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<AITopicResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed is not null)
                {
                    return new TopicDetails
                    {
                        Topic = topic,
                        Found = true,
                        Overview = parsed.Overview ?? "",
                        Content = parsed.Details ?? baseContent ?? "",
                        KeyConcepts = parsed.KeyConcepts ?? [],
                        UseCases = parsed.UseCases ?? [],
                        AiPowered = true
                    };
                }
            }
            return new TopicDetails { Topic = topic, Found = hasContent, Content = baseContent ?? content, AiPowered = false };
        }
        catch (Exception ex)
        {
            return new TopicDetails { Topic = topic, Content = "Error: " + ex.Message, AiPowered = false };
        }
    }

    [McpServerTool, Description("Gathers context from multiple topics for a complex task")]
    public static async Task<ContextResult> gather_context(
        [Description("Topics to research, comma-separated")] string topics)
    {
        var client = GetClient();
        var topicList = topics.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
        var allContext = new Dictionary<string, string>();
        foreach (var t in topicList)
            if (KnowledgeBase.TryGetValue(t.ToLower(), out var val))
                allContext[t] = val;

        var prompt = "Gather context for topics: " + string.Join(", ", topicList) + "\n\n" +
            "Knowledge:\n" + string.Join("\n", allContext.Select(c => "- " + c.Key + ": " + c.Value)) + "\n\n" +
            "Respond in JSON: {\"foundItems\": [{\"topic\": \"name\", \"content\": \"info\"}], \"synthesis\": \"combined context\", \"confidence\": 0.8}";

        try
        {
            var result = await client.GenerateAsync(prompt).StreamToEndAsync();
            var content = result?.Response ?? "";
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<AIContextResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed is not null)
                {
                    return new ContextResult
                    {
                        RequestedTopics = topicList,
                        FoundItems = parsed.FoundItems?.Select(f => new ContextItem { Topic = f.Topic ?? "", Content = f.Content ?? "" }).ToList() ?? [],
                        Synthesis = parsed.Synthesis ?? "",
                        Confidence = parsed.Confidence,
                        AiPowered = true
                    };
                }
            }
            return new ContextResult { RequestedTopics = topicList, Synthesis = content, Confidence = 0.5, AiPowered = false };
        }
        catch (Exception ex)
        {
            return new ContextResult { RequestedTopics = topicList, Synthesis = "Error: " + ex.Message, AiPowered = false };
        }
    }

    [McpServerTool, Description("Suggests related research topics")]
    public static async Task<TopicSuggestions> suggest_topics(
        [Description("The initial query")] string query)
    {
        var client = GetClient();
        var availableTopics = string.Join(", ", KnowledgeBase.Keys);

        var prompt = "Suggest research topics for query: \"" + query + "\"\n\n" +
            "Available topics: " + availableTopics + "\n\n" +
            "Respond in JSON: {\"suggestions\": [{\"topic\": \"name\", \"relevance\": 0.9, \"reason\": \"why\"}], \"researchPath\": \"recommended order\"}";

        try
        {
            var result = await client.GenerateAsync(prompt).StreamToEndAsync();
            var content = result?.Response ?? "";
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<AISuggestionsResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed is not null)
                {
                    return new TopicSuggestions
                    {
                        OriginalQuery = query,
                        Suggestions = parsed.Suggestions?.Select(s => new SuggestedTopic { Topic = s.Topic ?? "", Relevance = s.Relevance, Reason = s.Reason ?? "" }).ToList() ?? [],
                        ResearchPath = parsed.ResearchPath ?? "",
                        AiPowered = true
                    };
                }
            }
            return new TopicSuggestions { OriginalQuery = query, ResearchPath = content, AiPowered = false };
        }
        catch (Exception ex)
        {
            return new TopicSuggestions { OriginalQuery = query, ResearchPath = "Error: " + ex.Message, AiPowered = false };
        }
    }

    private static IOllamaApiClient GetClient()
    {
        if (_ollamaClient == null)
        {
            _ollamaClient = new OllamaApiClient("http://localhost:11434");
            _ollamaClient.SelectedModel = "llama3.1";
        }
        return _ollamaClient;
    }
}
