using AspireOllama.A2A.ResearchAgent.Models.A2a;
using AspireOllama.A2A.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.AsyncEnumerableExtensions;
using System.Text.Json;

namespace AspireOllama.A2A.ResearchAgent;

public class ResearchA2AServer : A2AServerBase
{
    private readonly Lazy<IOllamaApiClient> _ollama;
    private readonly KnownAgentsOptions _knownAgents;

    private static readonly Dictionary<string, string> KnowledgeBase = new()
    {
        ["dotnet"] = ".NET is a free, cross-platform developer platform for building many types of applications.",
        ["aspire"] = ".NET Aspire is an opinionated, cloud ready stack for building observable, production ready, distributed applications.",
        ["ollama"] = "Ollama is a tool for running large language models locally. It supports models like llama, mistral, llava.",
        ["mcp"] = "Model Context Protocol (MCP) is a standard for connecting AI models to external tools and data sources.",
        ["blazor"] = "Blazor is a framework for building interactive web UI with .NET instead of JavaScript.",
        ["a2a"] = "Agent-to-Agent (A2A) protocol enables AI agents to communicate and collaborate with each other."
    };

    public ResearchA2AServer(
        Lazy<IOllamaApiClient> ollamaClient,
        IOptions<KnownAgentsOptions> knownAgents,
        IA2AAgentClient a2aClient,
        ILogger<ResearchA2AServer> logger) : base(logger, a2aClient)
    {
        _ollama = ollamaClient;
        _knownAgents = knownAgents.Value;
    }

    public override AgentCard GetAgentCard() => new()
    {
        Name = "Research Agent",
        Description = "AI-powered research and knowledge gathering agent. Searches knowledge bases, synthesizes information, and provides comprehensive context for tasks.",
        Version = "2.0.0",
        Url = "http://research-agent",
        Provider = new AgentProvider { Organization = "AspireOllama" },
        Capabilities = new AgentCapabilities { Streaming = false, PushNotifications = false },
        Skills =
        [
            new AgentSkill
            {
                Id = "search_knowledge",
                Name = "Search Knowledge",
                Description = "Searches knowledge base and synthesizes relevant information",
                Tags = ["search", "knowledge", "research"],
                Examples = ["Search for information about .NET Aspire", "Find knowledge about MCP protocol"]
            },
            new AgentSkill
            {
                Id = "get_topic_details",
                Name = "Get Topic Details",
                Description = "Retrieves comprehensive details about a specific topic",
                Tags = ["details", "research", "information"],
                Examples = ["Get details about Blazor", "Explain Ollama in detail"]
            },
            new AgentSkill
            {
                Id = "gather_context",
                Name = "Gather Context",
                Description = "Gathers context from multiple topics for complex tasks",
                Tags = ["context", "synthesis", "multi-topic"],
                Examples = ["Gather context for building a web app with Blazor and Aspire"]
            },
            new AgentSkill
            {
                Id = "suggest_topics",
                Name = "Suggest Topics",
                Description = "Suggests related research topics based on a query",
                Tags = ["suggestions", "related", "exploration"],
                Examples = ["What topics should I research for AI development?"]
            }
        ]
    };

    public override async Task<A2ATask> ProcessMessageAsync(A2AMessage message, CancellationToken ct)
    {
        var task = CreateTask(message);
        UpdateTaskStatus(task, TaskState.Working, "Processing research request...");

        var text = GetTextFromMessage(message);
        _logger.LogInformation("Processing research request: {Text}", text.Length > 100 ? text[..100] + "..." : text);

        try
        {
            var lowerText = text.ToLowerInvariant();

            if (lowerText.Contains("detail") || lowerText.Contains("explain"))
            {
                await ProcessGetTopicDetails(task, text, ct);
            }
            else if (lowerText.Contains("context") || lowerText.Contains("gather"))
            {
                await ProcessGatherContext(task, text, ct);
            }
            else if (lowerText.Contains("suggest") || lowerText.Contains("related"))
            {
                await ProcessSuggestTopics(task, text, ct);
            }
            else
            {
                await ProcessSearchKnowledge(task, text, ct);
            }

            UpdateTaskStatus(task, TaskState.Completed, "Research completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing research request");
            UpdateTaskStatus(task, TaskState.Failed, $"Error: {ex.Message}");
        }

        return task;
    }

    private async Task ProcessSearchKnowledge(A2ATask task, string query, CancellationToken ct)
    {
        UpdateTaskStatus(task, TaskState.Working, "Searching knowledge base...", 30);

        // Find relevant entries from knowledge base
        var relevantEntries = KnowledgeBase
            .Where(kv => query.ToLower().Contains(kv.Key) || kv.Key.Contains(query.ToLower()) || kv.Value.ToLower().Contains(query.ToLower()))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var prompt = "You are a research assistant. Search for: \"" + query + "\"\n\n" +
            "Available knowledge:\n" + string.Join("\n", relevantEntries.Select(e => "- " + e.Key + ": " + e.Value)) + "\n\n" +
            "If the knowledge base doesn't have enough information, use your training data.\n\n" +
            "Respond in JSON: {\"matches\": [{\"topic\": \"name\", \"content\": \"info\", \"relevance\": 0.9}], \"synthesis\": \"comprehensive answer\", \"confidence\": 0.8}";

        var result = await _ollama.Value.GenerateAsync(prompt, cancellationToken: ct).StreamToEndAsync();
        var response = result?.Response ?? "";

        var searchResult = ParseJsonResponse<SearchResult>(response);
        if (searchResult is not null)
        {
            AddArtifact(task, searchResult, "Search Result");
            AddResponseToHistory(task, $"Found {searchResult.Matches?.Count ?? 0} matches. {searchResult.Synthesis}");
        }
        else
        {
            AddTextArtifact(task, response, "Search Response");
        }
    }

    private async Task ProcessGetTopicDetails(A2ATask task, string topic, CancellationToken ct)
    {
        UpdateTaskStatus(task, TaskState.Working, "Retrieving topic details...", 30);

        var baseContent = KnowledgeBase.FirstOrDefault(kv => topic.ToLower().Contains(kv.Key)).Value;

        var prompt = "Provide comprehensive details about: \"" + topic + "\"\n\n" +
            "Base knowledge: " + (baseContent ?? "Use your training data.") + "\n\n" +
            "Respond in JSON: {\"topic\": \"name\", \"overview\": \"summary\", \"details\": \"detailed explanation\", \"keyConcepts\": [], \"useCases\": [], \"relatedTopics\": []}";

        var result = await _ollama.Value.GenerateAsync(prompt, cancellationToken: ct).StreamToEndAsync();
        var response = result?.Response ?? "";

        var details = ParseJsonResponse<TopicDetailsResult>(response);
        if (details is not null)
        {
            AddArtifact(task, details, "Topic Details");
            AddResponseToHistory(task, $"Retrieved details for: {details.Topic}");
        }
        else
        {
            AddTextArtifact(task, response, "Topic Details Response");
        }
    }

    private async Task ProcessGatherContext(A2ATask task, string topics, CancellationToken ct)
    {
        UpdateTaskStatus(task, TaskState.Working, "Gathering context...", 30);

        var topicList = topics.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToLower())
            .Where(t => t.Length > 2)
            .ToList();

        var allContext = new Dictionary<string, string>();
        foreach (var t in topicList)
        {
            var match = KnowledgeBase.FirstOrDefault(kv => t.Contains(kv.Key) || kv.Key.Contains(t));
            if (!string.IsNullOrEmpty(match.Value))
                allContext[match.Key] = match.Value;
        }

        var prompt = "Gather and synthesize context for: " + topics + "\n\n" +
            "Available knowledge:\n" + string.Join("\n", allContext.Select(c => "- " + c.Key + ": " + c.Value)) + "\n\n" +
            "Respond in JSON: {\"topics\": [\"topic1\"], \"foundItems\": [{\"topic\": \"name\", \"content\": \"info\"}], \"synthesis\": \"combined context\", \"confidence\": 0.8, \"gaps\": [\"missing info\"]}";

        var result = await _ollama.Value.GenerateAsync(prompt, cancellationToken: ct).StreamToEndAsync();
        var response = result?.Response ?? "";

        var context = ParseJsonResponse<ContextResult>(response);
        if (context is not null)
        {
            AddArtifact(task, context, "Gathered Context");
            AddResponseToHistory(task, $"Gathered context for {context.FoundItems?.Count ?? 0} topics. Confidence: {context.Confidence:P0}");
        }
        else
        {
            AddTextArtifact(task, response, "Context Response");
        }
    }

    private async Task ProcessSuggestTopics(A2ATask task, string query, CancellationToken ct)
    {
        UpdateTaskStatus(task, TaskState.Working, "Suggesting related topics...", 30);

        var availableTopics = string.Join(", ", KnowledgeBase.Keys);

        var prompt = "Suggest research topics for: \"" + query + "\"\n\n" +
            "Available topics in knowledge base: " + availableTopics + "\n\n" +
            "Respond in JSON: {\"suggestions\": [{\"topic\": \"name\", \"relevance\": 0.9, \"reason\": \"why\"}], \"researchPath\": \"recommended order\", \"additionalSuggestions\": [\"external topics\"]}";

        var result = await _ollama.Value.GenerateAsync(prompt, cancellationToken: ct).StreamToEndAsync();
        var response = result?.Response ?? "";

        var suggestions = ParseJsonResponse<SuggestionsResult>(response);
        if (suggestions is not null)
        {
            AddArtifact(task, suggestions, "Topic Suggestions");
            AddResponseToHistory(task, $"Suggested {suggestions.Suggestions?.Count ?? 0} related topics");
        }
        else
        {
            AddTextArtifact(task, response, "Suggestions Response");
        }
    }

    private static T? ParseJsonResponse<T>(string content) where T : class
    {
        try
        {
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
        }
        catch { }
        return null;
    }
}
