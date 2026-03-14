using AspireOllama.ApiService.Services.Document;
using AspireOllama.Shared;
using Microsoft.Extensions.AI;

namespace AspireOllama.ApiService.Services.AI;

/// <summary>
/// Handles AI chat operations.
/// Single responsibility: Building context and calling the AI model.
/// </summary>
public class AiChatService : IAiChatService
{
    private readonly IChatClient _chatClient;
    private readonly IDocumentProcessingService _docService;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(
        IChatClient chatClient,
        IDocumentProcessingService docService,
        ILogger<AiChatService> logger)
    {
        _chatClient = chatClient;
        _docService = docService;
        _logger = logger;
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

        // Call AI model
        _logger.LogInformation("Calling AI model with {MessageCount} total messages", messages.Count);
        var response = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);

        _logger.LogInformation("Received response from AI model");
        return response.Text ?? string.Empty;
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
            if (msg.Role == "user" && msg.Files != null && msg.Files.Count > 0)
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
        if (request.Images != null && request.Images.Count > 0)
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
        if (request.Files != null && request.Files.Count > 0)
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
