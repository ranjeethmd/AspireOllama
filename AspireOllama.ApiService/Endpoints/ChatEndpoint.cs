using AspireOllama.ApiService.Services.AI;
using AspireOllama.ApiService.Services.Message;
using AspireOllama.ApiService.Services.Session;
using AspireOllama.Shared;
using FastEndpoints;
using System.Security.Claims;
using static AspireOllama.ServiceDefaults.Authentication.AuthRoles;

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
        Roles(ApiChatWrite);
    }

    public override async Task HandleAsync(ChatMessageRequest req, CancellationToken ct)
    {
        try
        {
            var userId = HttpContext.User.FindFirst("oid")?.Value
                ?? HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? "anonymous";

            // Verify session ownership before processing
            var session = await sessionService.GetByIdAsync(req.SessionId, userId);
            if (session is null)
            {
                await Send.NotFoundAsync(ct);
                return;
            }

            logger.LogInformation("Chat request for session {SessionId}, Images: {ImageCount}, Files: {FileCount}",
                req.SessionId, req.Images?.Count ?? 0, req.Files?.Count ?? 0);

            var history = await messageService.GetBySessionIdAsync(req.SessionId);
            var result = await aiService.GetResponseWithToolsAsync(req, history, ct);

            var historyContent = BuildHistoryContent(req);
            await messageService.AddAsync(req.SessionId, "user", historyContent, req.Images, req.Files);
            await messageService.AddAsync(req.SessionId, "assistant", result.Response);

            await UpdateSessionTitleIfFirstMessage(req, userId);
            await sessionService.TouchAsync(req.SessionId, userId);

            logger.LogInformation("Chat complete, {ToolCount} tool calls", result.ToolCalls.Count);

            await Send.OkAsync(new ChatMessageResponse
            {
                SessionId = req.SessionId,
                Response = result.Response,
                ToolCalls = result.ToolCalls,
                Usage = result.Usage
            }, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing chat request for session {SessionId}", req.SessionId);
            await Send.ResponseAsync(new ChatMessageResponse
            {
                SessionId = req.SessionId,
                Response = "An error occurred processing your request. Please try again."
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

    private async Task UpdateSessionTitleIfFirstMessage(ChatMessageRequest req, string userId)
    {
        var messageCount = await messageService.GetCountAsync(req.SessionId);

        if (messageCount == 2)
        {
            var title = !string.IsNullOrWhiteSpace(req.Content)
                ? req.Content
                : (req.Files?.FirstOrDefault()?.FileName
                   ?? req.Images?.FirstOrDefault()?.FileName
                   ?? "New Chat");

            await sessionService.UpdateTitleAsync(req.SessionId, userId, title);
        }
    }
}
