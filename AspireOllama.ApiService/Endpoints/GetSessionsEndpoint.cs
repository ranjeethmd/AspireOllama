using AspireOllama.ApiService.Services.Session;
using AspireOllama.Shared;
using FastEndpoints;
using System.Security.Claims;
using static AspireOllama.ServiceDefaults.Authentication.AuthRoles;

namespace AspireOllama.ApiService.Endpoints;

public class GetSessionsEndpoint : EndpointWithoutRequest<List<ChatSession>>
{
    private readonly ISessionService _sessionService;

    public GetSessionsEndpoint(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    public override void Configure()
    {
        Get("/sessions");
        Roles(ApiChatRead);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = HttpContext.User.FindFirst("oid")?.Value
            ?? HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? "anonymous";

        var sessions = await _sessionService.GetAllAsync(userId);
        await Send.OkAsync(sessions, ct);
    }
}
