using AspireOllama.ApiService.Services.AI;
using AspireOllama.ApiService.Services.Message;
using AspireOllama.ApiService.Services.Session;
using AspireOllama.Shared;
using FastEndpoints;

namespace AspireOllama.ApiService.Endpoints;

/// <summary>
/// Handles chat message requests.
/// Orchestrates session, message, and AI services.
/// </summary>
public class ChatEndpoint : Endpoint<ChatMessageRequest, ChatMessageResponse>
{
    private readonly ISessionService _sessionService;
    private readonly IChatMessageService _messageService;
    private readonly IAiChatService _aiService;
    private readonly ILogger<ChatEndpoint> _logger;

    public ChatEndpoint(
        ISessionService sessionService,
        IChatMessageService messageService,
        IAiChatService aiService,
        ILogger<ChatEndpoint> logger)
    {
        _sessionService = sessionService;
        _messageService = messageService;
        _aiService = aiService;
        _logger = logger;
    }

    /// <summary>
    /// Configures the endpoint route and access permissions.
    /// </summary>
    public override void Configure()
    {
        Post("/chat");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Send a chat message";
            s.Description = "Send a message with optional images and documents to the AI";
        });
    }

    /// <summary>
    /// Processes the chat request by orchestrating services.
    /// </summary>
    public override async Task HandleAsync(ChatMessageRequest req, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Chat request received for session {SessionId}", req.SessionId);

            // Get conversation history
            var history = await _messageService.GetBySessionIdAsync(req.SessionId);
            _logger.LogInformation("Loaded {Count} messages from history", history.Count);

            // Get AI response
            var responseText = await _aiService.GetResponseAsync(req, history, ct);

            // Build content for history storage (includes attachment metadata)
            var historyContent = BuildHistoryContent(req);

            // Save user message and AI response
            await _messageService.AddAsync(req.SessionId, "user", historyContent, req.Images, req.Files);
            await _messageService.AddAsync(req.SessionId, "assistant", responseText);

            // Update session title if this is the first message
            await UpdateSessionTitleIfFirstMessage(req);

            // Update session timestamp
            await _sessionService.TouchAsync(req.SessionId);

            await Send.OkAsync(new ChatMessageResponse
            {
                SessionId = req.SessionId,
                Response = responseText
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");

            await Send.ResponseAsync(new ChatMessageResponse
            {
                SessionId = req.SessionId,
                Response = $"Error: {ex.Message}"
            }, 500, ct);
        }
    }

    /// <summary>
    /// Builds content string for history storage with attachment metadata.
    /// </summary>
    private static string BuildHistoryContent(ChatMessageRequest req)
    {
        var content = req.Content ?? "";

        if (req.Images != null && req.Images.Count > 0)
        {
            var imageNames = string.Join(", ", req.Images.Select(i => i.FileName));
            content = string.IsNullOrWhiteSpace(content)
                ? $"[User shared image(s): {imageNames}]"
                : $"{content} [Attached image(s): {imageNames}]";
        }

        if (req.Files != null && req.Files.Count > 0)
        {
            var fileNames = string.Join(", ", req.Files.Select(f => f.FileName));
            content = string.IsNullOrWhiteSpace(content)
                ? $"[User shared document(s): {fileNames}]"
                : $"{content} [Attached document(s): {fileNames}]";
        }

        return content;
    }

    /// <summary>
    /// Updates session title from first user message.
    /// </summary>
    private async Task UpdateSessionTitleIfFirstMessage(ChatMessageRequest req)
    {
        var messageCount = await _messageService.GetCountAsync(req.SessionId);

        // Count is 2 because we just added user + assistant messages
        if (messageCount == 2)
        {
            var title = !string.IsNullOrWhiteSpace(req.Content)
                ? req.Content
                : (req.Files?.FirstOrDefault()?.FileName
                   ?? req.Images?.FirstOrDefault()?.FileName
                   ?? "New Chat");

            await _sessionService.UpdateTitleAsync(req.SessionId, title);
        }
    }
}
