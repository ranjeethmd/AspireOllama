using AspireOllama.ApiService.Services.Document;
using AspireOllama.ApiService.Services.Tools;
using AspireOllama.Shared;
using Microsoft.Extensions.AI;
using System.Diagnostics;

namespace AspireOllama.ApiService.Services.AI;

/// <summary>
/// AI chat service with two models behind a unified persona.
/// All tools are registered via IToolRegistry — AiChatService only orchestrates.
/// </summary>
public class AiChatService : IAiChatService
{
    private readonly IChatClient _chatClient;
    private readonly IChatClient _chatWithTools;
    private readonly IDocumentProcessingService _docService;
    private readonly IToolRegistry _toolRegistry;
    private readonly VisionTool _visionTool;
    private readonly ILogger<AiChatService> _logger;

    private static string SystemPrompt => $"""
        You are an intelligent AI agent. Today's date is {DateTime.UtcNow:MMMM d, yyyy}.

        IMPORTANT: Your training data has a knowledge cutoff. You do NOT have access to current events, news, or recent information.
        When users ask about news, current events, today's happenings, recent developments, or anything after your training cutoff — you MUST call web_search. Do NOT try to answer from memory.

        You have these tools (ALWAYS pass sessionId from the user's message):
        1. calculator(sessionId, expression) - Evaluate math expressions.
        2. search_knowledge_base(sessionId, query, topK) - Search uploaded documents. Results have relevance scores (0-1). Only use above 0.6.
        3. analyze_image(sessionId, instruction) - Analyze images from the conversation.
        4. web_search(sessionId, query, maxResults) - Search Google and Bing for current real-time information.

        WHEN TO USE TOOLS:
        - "what's the news today", "latest", "current events", "what happened" → ALWAYS call web_search
        - Any question about events after your training data → ALWAYS call web_search
        - User asks you to "search", "look up", "find out" → call web_search
        - Calculations, math → calculator
        - User uploads or asks about images → analyze_image
        - Questions about uploaded documents/files → search_knowledge_base (specific key terms, top_k=3)
        - General chat, greetings, questions you are CERTAIN about → respond directly

        WHEN NOT SURE: call web_search rather than guessing. Better to search and be accurate than to hallucinate.

        Rules for search_knowledge_base:
        - ONLY use results with relevance above 0.6. Ignore low-scoring noise.
        - Cite source file names.
        """;

    public AiChatService(
        [FromKeyedServices(OllamaModels.ChatServiceKey)] IChatClient chatClient,
        IDocumentProcessingService docService,
        IToolRegistry toolRegistry,
        VisionTool visionTool,
        ILogger<AiChatService> logger)
    {
        _chatClient = chatClient;
        _docService = docService;
        _toolRegistry = toolRegistry;
        _visionTool = visionTool;
        _logger = logger;

        _chatWithTools = new ChatClientBuilder(chatClient)
            .UseFunctionInvocation()
            .Build();
    }

    public async Task<string> GetResponseAsync(
        ChatMessageRequest request, List<ChatHistoryMessage> history,
        CancellationToken cancellationToken = default)
    {
        var messages = BuildHistoryMessages(history);
        if (!string.IsNullOrWhiteSpace(request.Content))
            messages.Add(new ChatMessage(ChatRole.User, request.Content));
        var response = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        return response.Text ?? string.Empty;
    }

    public async Task<AiChatResult> GetResponseWithToolsAsync(
        ChatMessageRequest request, List<ChatHistoryMessage> history,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var imageCount = request.Images?.Count ?? 0;
        var documentCount = request.Files?.Count ?? 0;

        var messages = BuildHistoryMessages(history);

        // Build user message with sessionId
        var userText = request.Content?.Trim();
        var sessionTag = $"[sessionId: {request.SessionId}]";

        if (imageCount > 0)
        {
            var imageNames = string.Join(", ", request.Images!.Select(i => i.FileName));
            var prompt = !string.IsNullOrWhiteSpace(userText)
                ? $"{sessionTag}\n[User uploaded {imageCount} image(s): {imageNames}]\n[DO NOT use search_knowledge_base. Use analyze_image only.]\n{userText}"
                : $"{sessionTag}\n[User uploaded {imageCount} image(s): {imageNames}]\n[DO NOT use search_knowledge_base. Use analyze_image only.]\nDescribe the uploaded image(s).";
            messages.Add(new ChatMessage(ChatRole.User, prompt));

            // Set current images on VisionTool so it can access them without DB round-trip
            _visionTool.SetCurrentImages(request.Images);
        }
        else if (documentCount > 0)
        {
            var docParts = new List<string>();
            foreach (var file in request.Files!)
            {
                var extracted = _docService.ExtractText(file);
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    var text = extracted.Length > 15000 ? extracted[..15000] + "\n\n[...truncated...]" : extracted;
                    docParts.Add($"[FILE: {file.FileName}]\n{text}\n[END FILE]");
                }
            }
            var question = !string.IsNullOrWhiteSpace(userText) ? userText : "Analyze the uploaded document(s).";
            messages.Add(new ChatMessage(ChatRole.User,
                $"{sessionTag}\n[DO NOT use search_knowledge_base. Analyze from content below.]\n\n{string.Join("\n\n", docParts)}\n\n{question}"));
        }
        else
        {
            messages.Add(new ChatMessage(ChatRole.User,
                $"{sessionTag}\n{(!string.IsNullOrWhiteSpace(userText) ? userText : "Hello")}"));
        }

        // Get all tools from the registry — no inline definitions
        var registryTools = _toolRegistry.GetEnabledTools();
        var chatOptions = new ChatOptions
        {
            Tools = registryTools.Cast<AITool>().ToList()
        };

        _logger.LogInformation("Calling Qwen3 with {Messages} messages, {Tools} tools (images: {Images})",
            messages.Count, registryTools.Count, imageCount);

        try
        {
            var response = await _chatWithTools.GetResponseAsync(messages, chatOptions, cancellationToken);
            stopwatch.Stop();

            var toolCalls = ExtractToolCalls(response);
            _logger.LogInformation("Qwen3 response in {Time}ms, {ToolCount} tool calls",
                stopwatch.ElapsedMilliseconds, toolCalls.Count);

            return new AiChatResult
            {
                Response = response.Text ?? string.Empty,
                ToolCalls = toolCalls,
                Usage = BuildUsageInfo(response, "qwen3", stopwatch.ElapsedMilliseconds, imageCount, documentCount, toolCalls)
            };
        }
        finally
        {
            _visionTool.ClearCurrentImages();
        }
    }

    private static List<ToolCall> ExtractToolCalls(ChatResponse response)
    {
        var toolCalls = new List<ToolCall>();
        foreach (var msg in response.Messages)
        {
            foreach (var content in msg.Contents)
            {
                if (content is FunctionCallContent fc)
                {
                    toolCalls.Add(new ToolCall
                    {
                        Id = fc.CallId ?? Guid.NewGuid().ToString(),
                        ToolName = fc.Name,
                        Arguments = fc.Arguments?.ToDictionary(k => k.Key, k => k.Value) ?? [],
                        Status = ToolCallStatus.Completed
                    });
                }
                if (content is FunctionResultContent fr)
                {
                    var existing = toolCalls.FirstOrDefault(t => t.Id == fr.CallId);
                    if (existing is not null)
                    {
                        var result = fr.Result?.ToString() ?? "";
                        existing.Result = result.Length > 200 ? result[..200] + "..." : result;
                    }
                }
            }
        }
        return toolCalls;
    }

    private List<ChatMessage> BuildHistoryMessages(List<ChatHistoryMessage> history)
    {
        var messages = new List<ChatMessage> { new(ChatRole.System, SystemPrompt) };
        foreach (var msg in history)
        {
            var role = msg.Role == "user" ? ChatRole.User : ChatRole.Assistant;
            messages.Add(new ChatMessage(role, msg.Content));
        }
        return messages;
    }

    private static UsageInfo BuildUsageInfo(
        ChatResponse response, string modelName, long responseTimeMs,
        int imageCount, int documentCount, List<ToolCall> toolCalls)
    {
        var usage = new UsageInfo
        {
            Model = modelName,
            ResponseTimeMs = responseTimeMs,
            ImagesProcessed = imageCount,
            DocumentsProcessed = documentCount,
            SkillCallsCount = toolCalls.Count,
            TotalSkillExecutionTimeMs = toolCalls.Sum(tc => tc.ExecutionTimeMs)
        };
        if (response.Usage is not null)
        {
            usage.PromptTokens = response.Usage.InputTokenCount ?? 0;
            usage.CompletionTokens = response.Usage.OutputTokenCount ?? 0;
        }
        if (response.AdditionalProperties is not null)
        {
            if (response.AdditionalProperties.TryGetValue("prompt_eval_duration", out var ped) && ped is long pedNs)
                usage.PromptEvalTimeMs = pedNs / 1_000_000;
            if (response.AdditionalProperties.TryGetValue("eval_duration", out var ed) && ed is long edNs)
                usage.EvalTimeMs = edNs / 1_000_000;
        }
        return usage;
    }
}
