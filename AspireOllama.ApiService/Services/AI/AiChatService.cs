using AspireOllama.ApiService.Services.Document;
using AspireOllama.ApiService.Services.Mcp;
using AspireOllama.ApiService.Services.Tools;
using AspireOllama.Shared;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AspireOllama.ApiService.Services.AI;

/// <summary>
/// Handles AI chat operations.
/// Single responsibility: Building context and calling the AI model.
/// Uses two models: llava for vision (images), llama3.1 for tool calling.
/// </summary>
public class AiChatService : IAiChatService
{
    private readonly IChatClient _visionClient;      // llava - for image understanding
    private readonly IChatClient _toolClient;        // llama3.1 - for tool calling
    private readonly IChatClient _functionInvokingClient;
    private readonly IDocumentProcessingService _docService;
    private readonly IToolRegistry _toolRegistry;
    private readonly IMcpService _mcpService;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(
        [FromKeyedServices("vision")] IChatClient visionClient,
        [FromKeyedServices("tools")] IChatClient toolClient,
        IDocumentProcessingService docService,
        IToolRegistry toolRegistry,
        IMcpService mcpService,
        ILogger<AiChatService> logger)
    {
        _visionClient = visionClient;
        _toolClient = toolClient;
        _docService = docService;
        _toolRegistry = toolRegistry;
        _mcpService = mcpService;
        _logger = logger;

        // Wrap the tool client with FunctionInvokingChatClient for automatic tool execution
        // This handles the tool call loop: model requests tool -> tool executes -> result sent back -> model continues
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

        // Build current user message with attachments
        var userMessage = BuildUserMessage(request);
        messages.Add(userMessage);

        // Choose model based on whether images are present
        var hasImages = request.Images is not null && request.Images.Count > 0;
        var client = hasImages ? _visionClient : _toolClient;
        var modelName = hasImages ? "llava (vision)" : "llama3.1 (tools)";

        // Call AI model
        _logger.LogInformation("Calling {Model} with {MessageCount} total messages", modelName, messages.Count);
        var response = await client.GetResponseAsync(messages, cancellationToken: cancellationToken);

        _logger.LogInformation("Received response from {Model}", modelName);
        return response.Text ?? string.Empty;
    }

    /// <inheritdoc />
    public async Task<AiChatResult> GetResponseWithToolsAsync(
        ChatMessageRequest request,
        List<ChatHistoryMessage> history,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Building AI context with tools enabled, {HistoryCount} history messages", history.Count);

        // Build conversation messages from history
        var messages = BuildHistoryMessages(history);

        // Build current user message with attachments
        var userMessage = BuildUserMessage(request);
        messages.Add(userMessage);

        // Check if images are present - use vision model without tools
        var hasImages = request.Images is not null && request.Images.Count > 0;

        if (hasImages)
        {
            // Vision model (llava) doesn't support tools - use it directly for images
            _logger.LogInformation("Images detected, using llava vision model (no tools)");
            var visionResponse = await _visionClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            return new AiChatResult
            {
                Response = visionResponse.Text ?? string.Empty,
                ToolCalls = new List<ToolCall>()
            };
        }

        // No images - use tool-calling model (llama3.1)
        // Collect all available tools from registry and MCP servers
        var tools = new List<AIFunction>();
        tools.AddRange(_toolRegistry.GetEnabledTools());

        try
        {
            var mcpTools = await _mcpService.GetMcpToolsAsync(cancellationToken);
            tools.AddRange(mcpTools);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get MCP tools, continuing without them");
        }

        _logger.LogInformation("Using llama3.1 with {Count} tools ({BuiltIn} built-in, {Mcp} from MCP)",
            tools.Count, _toolRegistry.GetEnabledTools().Count, tools.Count - _toolRegistry.GetEnabledTools().Count);

        // Build chat options with tools (cast AIFunction to AITool)
        var chatOptions = new ChatOptions
        {
            Tools = tools.Cast<AITool>().ToList()
        };

        // Track tool calls
        var toolCalls = new List<ToolCall>();
        var toolCallTimes = new Dictionary<string, DateTime>();

        // Call the function-invoking client which handles the tool execution loop
        _logger.LogInformation("Calling llama3.1 with {MessageCount} messages and {ToolCount} tools",
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

        _logger.LogInformation("Received response from AI model, {ToolCount} tool calls made", toolCalls.Count);

        return new AiChatResult
        {
            Response = response.Text ?? string.Empty,
            ToolCalls = toolCalls
        };
    }

    /// <summary>
    /// Builds chat messages from conversation history.
    /// Re-extracts document content for full context.
    /// </summary>
    private List<ChatMessage> BuildHistoryMessages(List<ChatHistoryMessage> history)
    {
        var messages = new List<ChatMessage>();

        foreach (var msg in history)
        {
            var role = msg.Role == "user" ? ChatRole.User : ChatRole.Assistant;
            var content = msg.Content;

            // Re-extract document content for historical messages
            // This ensures AI has full context even after app restart
            if (msg.Role == "user" && msg.Files is not null && msg.Files.Count > 0)
            {
                var docTexts = new List<string>();
                foreach (var file in msg.Files)
                {
                    var extractedText = _docService.ExtractText(file);
                    if (!string.IsNullOrWhiteSpace(extractedText))
                    {
                        docTexts.Add($"[Content from {file.FileName}]: {extractedText}");
                    }
                }
                if (docTexts.Count > 0)
                {
                    content = $"{content}\n\n{string.Join("\n\n", docTexts)}";
                }
            }

            messages.Add(new ChatMessage(role, content));
        }

        return messages;
    }

    /// <summary>
    /// Builds the current user message with images and documents.
    /// </summary>
    private ChatMessage BuildUserMessage(ChatMessageRequest request)
    {
        var contentParts = new List<AIContent>();

        // Process images - sent as binary data for vision model
        if (request.Images is not null && request.Images.Count > 0)
        {
            _logger.LogInformation("Processing {Count} images", request.Images.Count);
            foreach (var image in request.Images)
            {
                var imageBytes = Convert.FromBase64String(image.Base64Data);
                var mediaType = NormalizeMediaType(image.ContentType);

                _logger.LogInformation("Adding image: {FileName}, MediaType: {MediaType}, Size: {Size} bytes",
                    image.FileName, mediaType, imageBytes.Length);

                contentParts.Add(new DataContent(imageBytes, mediaType));
            }
        }

        // Process documents - extract text content
        var documentTexts = new List<string>();
        if (request.Files is not null && request.Files.Count > 0)
        {
            _logger.LogInformation("Processing {Count} documents", request.Files.Count);
            foreach (var file in request.Files)
            {
                _logger.LogInformation("Extracting text from: {FileName}, Type: {Type}", file.FileName, file.Type);
                var extractedText = _docService.ExtractText(file);
                if (!string.IsNullOrWhiteSpace(extractedText))
                {
                    documentTexts.Add($"--- Content from {file.FileName} ---\n{extractedText}\n--- End of {file.FileName} ---");
                }
            }
        }

        // Compose final text content
        var textContent = ComposeTextContent(request.Content, documentTexts, request.Images?.Count ?? 0);
        contentParts.Add(new TextContent(textContent));

        return new ChatMessage(ChatRole.User, contentParts);
    }

    /// <summary>
    /// Composes the final text content combining user message and document text.
    /// </summary>
    private static string ComposeTextContent(string? userContent, List<string> documentTexts, int imageCount)
    {
        var textContent = userContent ?? "";

        if (documentTexts.Count > 0)
        {
            var documentContext = string.Join("\n\n", documentTexts);
            textContent = string.IsNullOrWhiteSpace(textContent)
                ? $"Please analyze the following document(s):\n\n{documentContext}"
                : $"{textContent}\n\nDocument content:\n\n{documentContext}";
        }
        else if (string.IsNullOrWhiteSpace(textContent) && imageCount > 0)
        {
            // Default prompt for image-only messages
            textContent = "Describe this image";
        }

        return textContent;
    }

    /// <summary>
    /// Normalizes media type for Ollama compatibility.
    /// </summary>
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
