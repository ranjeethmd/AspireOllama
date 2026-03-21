using AspireOllama.ApiService.Services.Session;
using AspireOllama.Shared;
using FastEndpoints;
using System.Security.Claims;
using static AspireOllama.ServiceDefaults.Authentication.AuthRoles;

namespace AspireOllama.ApiService.Endpoints;

public class CreateSessionEndpoint : EndpointWithoutRequest<ChatSession>
{
    private readonly ISessionService _sessionService;

    public CreateSessionEndpoint(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    public override void Configure()
    {
        Post("/sessions");
        Roles(ApiSessionsManage);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = HttpContext.User.FindFirst("oid")?.Value
            ?? HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? "anonymous";
        var userName = HttpContext.User.FindFirst("name")?.Value
            ?? HttpContext.User.FindFirst(ClaimTypes.Name)?.Value
            ?? "Unknown";

        var session = await _sessionService.CreateAsync(userId, userName);
        await Send.OkAsync(session, ct);
    }
}
