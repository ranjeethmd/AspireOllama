using AspireOllama.ApiService.Services.Document;
using AspireOllama.ApiService.Services.Message;
using AspireOllama.ApiService.Services.Rag;
using AspireOllama.Shared;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Diagnostics;

namespace AspireOllama.ApiService.Services.AI;

/// <summary>
/// AI chat service with two models behind a unified persona:
/// - Qwen3: primary chat model with tool calling (RAG + image analysis)
/// - Qwen2.5-VL: vision model called via analyze_image tool
/// The user sees one assistant. Qwen3 decides when to search docs or analyze images.
/// </summary>
public class AiChatService : IAiChatService
{
    private readonly IChatClient _chatClient;
    private readonly IChatClient _visionClient;
    private readonly IChatClient _chatWithTools;
    private readonly IDocumentProcessingService _docService;
    private readonly IRagRetrievalService _ragService;
    private readonly IChatMessageService _messageService;
    private readonly ILogger<AiChatService> _logger;

    private const string SystemPrompt = """
        You are an intelligent AI agent capable of analyzing images, searching documents, and having conversations.

        You have these tools:
        1. search_knowledge_base(session_id, query, top_k) - Search uploaded documents for information. Returns results with relevance scores.
        2. analyze_image(session_id, instruction) - Analyze images from the conversation.

        ALWAYS pass the session_id to every tool call. The session_id is provided in the user's message.

        When to use tools:
        - User uploads images or asks about images → call analyze_image
        - User asks follow-up questions about previously uploaded images → call analyze_image again
        - User asks about document content, reports, or uploaded files → call search_knowledge_base with specific key terms and top_k=3
        - General chat, greetings, or questions you know the answer to → do NOT use tools

        IMPORTANT rules for search_knowledge_base:
        - Each result has a relevance score (0.0 to 1.0). ONLY use results with relevance above 0.6.
        - IGNORE low-scoring results — they are noise, NOT relevant to the question.
        - If all results score below 0.6, tell the user the knowledge base doesn't contain relevant information for their question.
        - Use specific search queries with key terms extracted from the user's question, NOT the full user message.
        - Cite the source file name when using results.

        Capabilities you should confirm when asked:
        - You CAN analyze images, describe photos, read text in images, identify objects
        - You CAN search uploaded documents and knowledge bases
        - You CAN have multi-turn conversations about images and documents
        """;

    public AiChatService(
        [FromKeyedServices(OllamaModels.ChatServiceKey)] IChatClient chatClient,
        [FromKeyedServices(OllamaModels.VisionServiceKey)] IChatClient visionClient,
        IDocumentProcessingService docService,
        IRagRetrievalService ragService,
        IChatMessageService messageService,
        ILogger<AiChatService> logger)
    {
        _chatClient = chatClient;
        _visionClient = visionClient;
        _docService = docService;
        _ragService = ragService;
        _messageService = messageService;
        _logger = logger;

        _chatWithTools = new ChatClientBuilder(chatClient)
            .UseFunctionInvocation()
            .Build();
    }

    public async Task<string> GetResponseAsync(
        ChatMessageRequest request,
        List<ChatHistoryMessage> history,
        CancellationToken cancellationToken = default)
    {
        var messages = BuildHistoryMessages(history);
        if (!string.IsNullOrWhiteSpace(request.Content))
            messages.Add(new ChatMessage(ChatRole.User, request.Content));

        var response = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        return response.Text ?? string.Empty;
    }

    public async Task<AiChatResult> GetResponseWithToolsAsync(
        ChatMessageRequest request,
        List<ChatHistoryMessage> history,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var imageCount = request.Images?.Count ?? 0;
        var documentCount = request.Files?.Count ?? 0;

        var messages = BuildHistoryMessages(history);

        // Build user message with session_id so Qwen3 can pass it to tools
        var userText = request.Content?.Trim();
        var sessionTag = $"[session_id: {request.SessionId}]";

        if (imageCount > 0)
        {
            // Image upload — use analyze_image only, no RAG
            var imageNames = string.Join(", ", request.Images!.Select(i => i.FileName));
            var prompt = !string.IsNullOrWhiteSpace(userText)
                ? $"{sessionTag}\n[User uploaded {imageCount} image(s): {imageNames}]\n[DO NOT use search_knowledge_base. Use analyze_image only.]\n{userText}"
                : $"{sessionTag}\n[User uploaded {imageCount} image(s): {imageNames}]\n[DO NOT use search_knowledge_base. Use analyze_image only.]\nDescribe the uploaded image(s).";
            messages.Add(new ChatMessage(ChatRole.User, prompt));
        }
        else if (documentCount > 0)
        {
            // Document upload via chat — extract text inline, no RAG
            // The user wants THIS file analyzed, not the knowledge base searched
            var docParts = new List<string>();
            foreach (var file in request.Files!)
            {
                var extracted = _docService.ExtractText(file);
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    // Truncate very large documents to avoid context overflow
                    var text = extracted.Length > 15000 ? extracted[..15000] + "\n\n[...document truncated...]" : extracted;
                    docParts.Add($"[FILE: {file.FileName}]\n{text}\n[END FILE]");
                    _logger.LogInformation("Extracted {Chars} chars from {FileName}", extracted.Length, file.FileName);
                }
            }

            var question = !string.IsNullOrWhiteSpace(userText) ? userText : "Analyze the uploaded document(s).";
            var docPrompt = $"{sessionTag}\n[DO NOT use search_knowledge_base. The user uploaded files directly — analyze them from the content below.]\n\n" +
                            $"{string.Join("\n\n", docParts)}\n\n{question}";
            messages.Add(new ChatMessage(ChatRole.User, docPrompt));
        }
        else
        {
            // Text only — tools available as normal
            var text = !string.IsNullOrWhiteSpace(userText) ? userText : "Hello";
            messages.Add(new ChatMessage(ChatRole.User, $"{sessionTag}\n{text}"));
        }

        // Register tools
        var tools = new List<AITool>();

        // RAG tool — includes relevance scores so the LLM can filter noise
        tools.Add(AIFunctionFactory.Create(
            [Description("Search the uploaded knowledge base for relevant information. Returns results ranked by relevance score (0-1). Only use results with high relevance (above 0.6) to answer questions. Ignore low-relevance results as noise.")]
            async (
                [Description("The chat session ID")] string session_id,
                [Description("The search query — be specific and use key terms from the user's question")] string query,
                [Description("Number of results to return (1-10, default 3)")] int top_k = 3) =>
            {
                _logger.LogInformation("RAG tool: session={SessionId}, query={Query}, topK={TopK}", session_id, query, top_k);
                var clampedK = Math.Clamp(top_k, 1, 10);
                var chunks = await _ragService.SearchAsync(query, topK: clampedK, ct: cancellationToken);
                if (chunks.Count == 0)
                    return "No relevant documents found in the knowledge base.";
                return $"Found {chunks.Count} results (ranked by relevance):\n\n" +
                    string.Join("\n\n", chunks.Select(c =>
                        $"[Relevance: {c.Score:F2}] [{c.FileName}, section {c.ChunkIndex}]:\n{c.Text}"));
            },
            "search_knowledge_base"));

        // Image analysis tool — retrieves images from session history, sends to Qwen2.5-VL
        tools.Add(AIFunctionFactory.Create(
            [Description("Analyze images from the conversation. Retrieves the most recent images from the chat session and answers questions about them.")]
            async (
                [Description("The chat session ID")] string session_id,
                [Description("What to analyze or describe about the image(s)")] string instruction) =>
            {
                _logger.LogInformation("Image tool: session={SessionId}, instruction={Instruction}", session_id, instruction);

                List<ImageAttachment> images;
                if (request.Images is { Count: > 0 })
                {
                    images = request.Images;
                }
                else
                {
                    var sessionHistory = await _messageService.GetBySessionIdAsync(session_id);
                    var lastImageMsg = sessionHistory.LastOrDefault(m => m.Images.Count > 0);
                    if (lastImageMsg is null)
                        return "No images found in this conversation. Ask the user to upload an image.";
                    images = lastImageMsg.Images;
                }

                var contentParts = new List<AIContent>();
                foreach (var img in images)
                {
                    var data = Convert.FromBase64String(img.Base64Data);
                    contentParts.Add(new DataContent(data, NormalizeMediaType(img.ContentType)));
                }
                contentParts.Add(new TextContent(instruction));

                var visionMessages = new List<ChatMessage> { new(ChatRole.User, contentParts) };

                _logger.LogInformation("Sending {Count} images to Qwen2.5-VL", images.Count);
                var visionResponse = await _visionClient.GetResponseAsync(visionMessages, cancellationToken: cancellationToken);
                return visionResponse.Text ?? "Unable to analyze the image.";
            },
            "analyze_image"));

        var chatOptions = new ChatOptions { Tools = tools };

        _logger.LogInformation("Calling Qwen3 with {Count} messages, {ToolCount} tools (images: {ImageCount})",
            messages.Count, tools.Count, imageCount);

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

    private static string NormalizeMediaType(string contentType) => contentType switch
    {
        "image/jpeg" or "image/jpg" => "image/jpeg",
        "image/png" => "image/png",
        "image/gif" => "image/gif",
        "image/webp" => "image/webp",
        _ => "image/jpeg"
    };
}
