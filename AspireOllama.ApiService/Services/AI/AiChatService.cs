using AspireOllama.ApiService.Services.Document;
using AspireOllama.ApiService.Services.Mcp;
using AspireOllama.ApiService.Services.Rag;
using AspireOllama.ApiService.Services.Tools;
using AspireOllama.Shared;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace AspireOllama.ApiService.Services.AI;

/// <summary>
/// Handles AI chat operations.
/// Single responsibility: Building context and calling the AI model.
/// Llama 3 is the main agent that can delegate to LLaVA via the image analysis tool.
/// </summary>
public class AiChatService : IAiChatService
{
    private readonly IChatClient _toolClient;
    private readonly IChatClient _functionInvokingClient;
    private readonly IDocumentProcessingService _docService;
    private readonly IRagRetrievalService _ragService;
    private readonly IToolRegistry _toolRegistry;
    private readonly IMcpService _mcpService;
    private readonly ImageAnalysisTool _imageAnalysisTool;
    private readonly ILogger<AiChatService> _logger;

    /// <summary>
    /// System prompt that guides the AI's behavior.
    /// </summary>
    private const string SystemPrompt = """
        You are a helpful AI assistant. Respond ONLY in plain natural language.

        ABSOLUTE RULES:
        1. NEVER output JSON, code blocks, or structured data in your response unless the user explicitly asks for code.
        2. NEVER write {"name":...} or any function call syntax. The system handles tools automatically.
        3. When you see [RELEVANT CONTEXT from uploaded documents], use that information to answer naturally. Cite the source file when relevant.
        4. If the context doesn't contain enough information, say so honestly.
        5. When images are attached, describe what you see or answer questions about them.
        6. Respond conversationally like a helpful human assistant would.
        """;

    public AiChatService(
        [FromKeyedServices("tools")] IChatClient toolClient,
        IDocumentProcessingService docService,
        IRagRetrievalService ragService,
        IToolRegistry toolRegistry,
        IMcpService mcpService,
        ImageAnalysisTool imageAnalysisTool,
        ILogger<AiChatService> logger)
    {
        _toolClient = toolClient;
        _docService = docService;
        _ragService = ragService;
        _toolRegistry = toolRegistry;
        _mcpService = mcpService;
        _imageAnalysisTool = imageAnalysisTool;
        _logger = logger;

        // Wrap the tool client with FunctionInvokingChatClient for automatic tool execution
        _functionInvokingClient = new ChatClientBuilder(toolClient)
            .UseFunctionInvocation()
            .Build();
    }

    /// <inheritdoc />
    public async Task<string> GetResponseAsync(
        ChatMessageRequest request,
        List<ChatHistoryMessage> history,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Building AI context with {HistoryCount} history messages", history.Count);

        // Build conversation messages from history
        var messages = BuildHistoryMessages(history);

        // Build current user message with RAG context
        var userMessage = await BuildUserMessageAsync(request, cancellationToken);
        messages.Add(userMessage);

        // Always use Llama 3 as the main agent
        _logger.LogInformation("Calling llama3 with {MessageCount} total messages (including system prompt)", messages.Count);
        var response = await _toolClient.GetResponseAsync(messages, cancellationToken: cancellationToken);

        _logger.LogInformation("Received response from llama3");
        return response.Text ?? string.Empty;
    }

    /// <inheritdoc />
    public async Task<AiChatResult> GetResponseWithToolsAsync(
        ChatMessageRequest request,
        List<ChatHistoryMessage> history,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Building AI context with tools enabled, {HistoryCount} history messages", history.Count);
        var stopwatch = Stopwatch.StartNew();

        // Build conversation messages from history
        var messages = BuildHistoryMessages(history);

        // Build current user message with RAG context
        var userMessage = await BuildUserMessageAsync(request, cancellationToken);
        messages.Add(userMessage);

        var imageCount = request.Images?.Count ?? 0;
        var documentCount = request.Files?.Count ?? 0;

        // Set up pending images for the ImageAnalysisTool
        if (request.Images is not null && request.Images.Count > 0)
        {
            var pendingImages = request.Images.Select(img => new PendingImage
            {
                FileName = img.FileName,
                Data = Convert.FromBase64String(img.Base64Data),
                MediaType = NormalizeMediaType(img.ContentType)
            }).ToList();

            _imageAnalysisTool.SetImages(pendingImages);
        }

        try
        {
            // For document-only requests, don't pass tools to avoid JSON output hallucination
            // Only pass tools when images are present (need analyze_image) or explicit tool use is expected
            var needsTools = imageCount > 0;

            if (!needsTools)
            {
                // No tools needed - use direct chat without function calling
                _logger.LogInformation("Document-only request, using direct chat (no tools)");

                var directResponse = await _toolClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
                stopwatch.Stop();

                return new AiChatResult
                {
                    Response = SanitizeResponse(directResponse.Text ?? string.Empty),
                    ToolCalls = new List<ToolCall>(),
                    Usage = BuildUsageInfo(directResponse, "llama3", stopwatch.ElapsedMilliseconds, imageCount, documentCount, new List<ToolCall>())
                };
            }

            // Collect tools - only when needed (images present)
            var tools = new List<AIFunction>();
            var registryTools = _toolRegistry.GetEnabledTools();

            foreach (var tool in registryTools)
            {
                // Only include analyze_image when images are present
                if (tool.Name == "analyze_image" && imageCount == 0)
                    continue;

                // Skip document analysis tool - we use direct extraction instead
                if (tool.Name == "analyze_document")
                    continue;

                tools.Add(tool);
            }

            try
            {
                var mcpTools = await _mcpService.GetMcpToolsAsync(cancellationToken);
                tools.AddRange(mcpTools);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get MCP tools, continuing without them");
            }

            _logger.LogInformation("Using llama3 with {Count} tools (images: {HasImages})",
                tools.Count, imageCount > 0);

            // Build chat options with tools (cast AIFunction to AITool)
            var chatOptions = new ChatOptions
            {
                Tools = tools.Cast<AITool>().ToList()
            };

            // Track tool calls
            var toolCalls = new List<ToolCall>();
            var toolCallTimes = new Dictionary<string, DateTime>();

            // Call the function-invoking client which handles the tool execution loop
            _logger.LogInformation("Calling llama3 with {MessageCount} messages (including system prompt) and {ToolCount} tools",
                messages.Count, tools.Count);

            var response = await _functionInvokingClient.GetResponseAsync(
                messages,
                chatOptions,
                cancellationToken);

            // Extract tool call information from response messages
            foreach (var message in response.Messages)
            {
                foreach (var content in message.Contents)
                {
                    if (content is FunctionCallContent functionCall)
                    {
                        var toolCall = new ToolCall
                        {
                            Id = functionCall.CallId ?? Guid.NewGuid().ToString(),
                            ToolName = functionCall.Name,
                            Arguments = functionCall.Arguments?.ToDictionary(
                                kvp => kvp.Key,
                                kvp => kvp.Value) ?? new Dictionary<string, object?>(),
                            Status = ToolCallStatus.Executing,
                            ExecutionTimeMs = 0
                        };

                        toolCalls.Add(toolCall);
                        toolCallTimes[toolCall.Id] = DateTime.UtcNow;

                        _logger.LogInformation("Tool call: {ToolName} with args: {Args}",
                            functionCall.Name,
                            string.Join(", ", toolCall.Arguments.Select(a => $"{a.Key}={a.Value}")));
                    }
                    else if (content is FunctionResultContent functionResult)
                    {
                        var existingCall = toolCalls.FirstOrDefault(tc => tc.Id == functionResult.CallId);
                        if (existingCall is not null)
                        {
                            existingCall.Result = functionResult.Result?.ToString() ?? "(no result)";
                            existingCall.Status = ToolCallStatus.Completed;

                            if (toolCallTimes.TryGetValue(existingCall.Id, out var startTime))
                            {
                                existingCall.ExecutionTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
                            }

                            _logger.LogInformation("Tool result for {ToolName}: {Result} ({Time}ms)",
                                existingCall.ToolName, existingCall.Result, existingCall.ExecutionTimeMs);
                        }
                    }
                }
            }

            stopwatch.Stop();
            _logger.LogInformation("Received response from llama3, {ToolCount} tool calls made", toolCalls.Count);

            return new AiChatResult
            {
                Response = SanitizeResponse(response.Text ?? string.Empty),
                ToolCalls = toolCalls,
                Usage = BuildUsageInfo(response, "llama3", stopwatch.ElapsedMilliseconds, imageCount, documentCount, toolCalls)
            };
        }
        finally
        {
            // Clear pending images after request completion
            _imageAnalysisTool.ClearImages();
        }
    }

    /// <summary>
    /// Builds usage information from the AI response.
    /// </summary>
    private static UsageInfo BuildUsageInfo(
        ChatResponse response,
        string modelName,
        long responseTimeMs,
        int imageCount,
        int documentCount,
        List<ToolCall> toolCalls)
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

        // Extract token usage from response if available
        if (response.Usage is not null)
        {
            usage.PromptTokens = response.Usage.InputTokenCount ?? 0;
            usage.CompletionTokens = response.Usage.OutputTokenCount ?? 0;
        }

        // Try to extract additional timing from response metadata
        if (response.AdditionalProperties is not null)
        {
            if (response.AdditionalProperties.TryGetValue("prompt_eval_duration", out var promptEvalDuration) &&
                promptEvalDuration is long promptEvalNs)
            {
                usage.PromptEvalTimeMs = promptEvalNs / 1_000_000; // Convert nanoseconds to milliseconds
            }

            if (response.AdditionalProperties.TryGetValue("eval_duration", out var evalDuration) &&
                evalDuration is long evalNs)
            {
                usage.EvalTimeMs = evalNs / 1_000_000; // Convert nanoseconds to milliseconds
            }
        }

        return usage;
    }

    /// <summary>
    /// Builds chat messages from conversation history.
    /// Includes system prompt and re-extracts document content for full context.
    /// </summary>
    private List<ChatMessage> BuildHistoryMessages(List<ChatHistoryMessage> history)
    {
        var messages = new List<ChatMessage>
        {
            // Add system prompt at the beginning to guide AI behavior
            new(ChatRole.System, SystemPrompt)
        };

        foreach (var msg in history)
        {
            var role = msg.Role == "user" ? ChatRole.User : ChatRole.Assistant;
            var content = msg.Content;

            // Document content is now in the RAG vector store — no need to re-extract
            messages.Add(new ChatMessage(role, content));
        }

        return messages;
    }

    /// <summary>
    /// Builds the current user message with images and RAG-retrieved document context.
    /// Documents are already ingested into the vector store by ChatEndpoint.
    /// RAG retrieval finds relevant chunks based on the user's query.
    /// </summary>
    private async Task<ChatMessage> BuildUserMessageAsync(ChatMessageRequest request, CancellationToken ct)
    {
        var contentParts = new List<AIContent>();

        // Build image info for Llama 3 (it will use analyze_image tool)
        var imageInfo = new List<string>();
        if (request.Images is not null && request.Images.Count > 0)
        {
            foreach (var image in request.Images)
            {
                imageInfo.Add($"- {image.FileName}");
            }
            _logger.LogInformation("Images available for analysis: {Count}", request.Images.Count);
        }

        // RAG: Retrieve relevant document chunks via vector similarity search
        var ragContext = new List<string>();
        var query = request.Content ?? "Analyze the uploaded documents.";
        var retrievedChunks = await _ragService.SearchAsync(query, topK: 5, ct: ct);
        if (retrievedChunks.Count > 0)
        {
            _logger.LogInformation("RAG retrieved {Count} relevant chunks for query", retrievedChunks.Count);
            foreach (var chunk in retrievedChunks)
            {
                ragContext.Add($"[From {chunk.FileName}, section {chunk.ChunkIndex}]: {chunk.Text}");
            }
        }

        // Compose final text content
        var textContent = ComposeTextContent(request.Content, imageInfo, ragContext);
        contentParts.Add(new TextContent(textContent));

        return new ChatMessage(ChatRole.User, contentParts);
    }

    /// <summary>
    /// Composes the final text content with images info and RAG context.
    /// </summary>
    private static string ComposeTextContent(string? userContent, List<string> imageInfo, List<string> ragContext)
    {
        var parts = new List<string>();

        // Add RAG context first so the model sees it before the question
        if (ragContext.Count > 0)
        {
            parts.Add($"[RELEVANT CONTEXT from uploaded documents]\n{string.Join("\n\n", ragContext)}\n[END CONTEXT]");
        }

        // Add user message
        if (!string.IsNullOrWhiteSpace(userContent))
        {
            parts.Add(userContent);
        }

        // Add image availability info (requires tool to view)
        if (imageInfo.Count > 0)
        {
            parts.Add($"[IMAGES - Use analyze_image tool to view]\n{string.Join("\n", imageInfo)}");
        }

        // Default prompt if only files uploaded with no message
        if (parts.Count == 0)
        {
            return "Please analyze the uploaded file(s).";
        }

        return string.Join("\n\n", parts);
    }

    /// <summary>
    /// Normalizes media type for Ollama compatibility.
    /// </summary>
    /// <summary>
    /// Strips hallucinated JSON function calls from the LLM response.
    /// llama3.1 sometimes outputs {"name": "...", "parameters": {...}} as text
    /// instead of making a proper tool call.
    /// </summary>
    private static string SanitizeResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return response;

        // Remove JSON blocks that look like function calls
        var sanitized = System.Text.RegularExpressions.Regex.Replace(
            response,
            @"\{""name""\s*:\s*""[^""]+"".*?""parameters""\s*:\s*\{.*?\}\s*\}",
            "",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        // Clean up leftover text around the removed JSON
        sanitized = sanitized.Trim();

        // If the entire response was just a function call JSON, return a helpful message
        if (string.IsNullOrWhiteSpace(sanitized))
            return "I processed your request. Could you please rephrase your question?";

        return sanitized;
    }

    private static string NormalizeMediaType(string contentType)
    {
        return contentType switch
        {
            "image/jpeg" or "image/jpg" => "image/jpeg",
            "image/png" => "image/png",
            "image/gif" => "image/gif",
            "image/webp" => "image/webp",
            _ => "image/jpeg"
        };
    }
}
