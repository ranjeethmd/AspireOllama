using AspireOllama.ApiService.Services.AI;
using AspireOllama.ApiService.Services.Message;
using AspireOllama.ApiService.Services.Session;
using AspireOllama.Shared;
using FastEndpoints;

namespace AspireOllama.ApiService.Endpoints;

public class ChatEndpoint(
    ISessionService sessionService,
    IChatMessageService messageService,
    IAiChatService aiService,
    ILogger<ChatEndpoint> logger) : Endpoint<ChatMessageRequest, ChatMessageResponse>
{
    public override void Configure()
    {
        Post("/chat");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChatMessageRequest req, CancellationToken ct)
    {
        try
        {
            logger.LogInformation("Chat request for session {SessionId}", req.SessionId);

            var history = await messageService.GetBySessionIdAsync(req.SessionId);
            var result = await aiService.GetResponseWithToolsAsync(req, history, ct);

            var historyContent = BuildHistoryContent(req);
            await messageService.AddAsync(req.SessionId, "user", historyContent, req.Images, req.Files);
            await messageService.AddAsync(req.SessionId, "assistant", result.Response);

            await UpdateSessionTitleIfFirstMessage(req);
            await sessionService.TouchAsync(req.SessionId);

            logger.LogInformation("Chat complete, {ToolCount} tool calls", result.ToolCalls.Count);

            await Send.OkAsync(new ChatMessageResponse
            {
                SessionId = req.SessionId,
                Response = result.Response,
                ToolCalls = result.ToolCalls
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing chat request");
            await Send.ResponseAsync(new ChatMessageResponse
            {
                SessionId = req.SessionId,
                Response = $"Error: {ex.Message}"
            }, 500, ct);
        }
    }

    private static string BuildHistoryContent(ChatMessageRequest req)
    {
        var content = req.Content ?? "";

        if (req.Images is { Count: > 0 })
        {
            var imageNames = string.Join(", ", req.Images.Select(i => i.FileName));
            content = string.IsNullOrWhiteSpace(content)
                ? $"[Images: {imageNames}]"
                : $"{content} [Images: {imageNames}]";
        }

        if (req.Files is { Count: > 0 })
        {
            var fileNames = string.Join(", ", req.Files.Select(f => f.FileName));
            content = string.IsNullOrWhiteSpace(content)
                ? $"[Documents: {fileNames}]"
                : $"{content} [Documents: {fileNames}]";
        }

        return content;
    }

    private async Task UpdateSessionTitleIfFirstMessage(ChatMessageRequest req)
    {
        var messageCount = await messageService.GetCountAsync(req.SessionId);

        if (messageCount == 2)
        {
            var title = !string.IsNullOrWhiteSpace(req.Content)
                ? req.Content
                : (req.Files?.FirstOrDefault()?.FileName
                   ?? req.Images?.FirstOrDefault()?.FileName
                   ?? "New Chat");

            await sessionService.UpdateTitleAsync(req.SessionId, title);
        }
    }
}
