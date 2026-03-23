using AspireOllama.ApiService.Services.Session;
using FastEndpoints;
using System.Security.Claims;
using static AspireOllama.ServiceDefaults.Authentication.AuthRoles;

namespace AspireOllama.ApiService.Endpoints;

/// <summary>
/// Request DTO for deleting a session.
/// </summary>
public class DeleteSessionRequest
{
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Deletes a chat session and all its messages.
/// </summary>
public class DeleteSessionEndpoint : Endpoint<DeleteSessionRequest>
{
    private readonly ISessionService _sessionService;

    public DeleteSessionEndpoint(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    public override void Configure()
    {
        Delete("/sessions/{Id}");
        Roles(ApiSessionsManage);
        Summary(s =>
        {
            s.Summary = "Delete a chat session";
            s.Description = "Deletes a chat session and all its messages";
        });
    }

    public override async Task HandleAsync(DeleteSessionRequest req, CancellationToken ct)
    {
        var userId = HttpContext.User.FindFirst("oid")?.Value
            ?? HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? "anonymous";

        var deleted = await _sessionService.DeleteAsync(req.Id, userId);

        if (!deleted)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}
