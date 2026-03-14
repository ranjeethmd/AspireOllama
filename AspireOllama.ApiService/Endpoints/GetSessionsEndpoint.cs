using AspireOllama.ApiService.Services.Session;
using AspireOllama.Shared;
using FastEndpoints;

namespace AspireOllama.ApiService.Endpoints;

/// <summary>
/// Retrieves all chat sessions.
/// </summary>
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
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get all chat sessions";
            s.Description = "Returns a list of all chat sessions ordered by last updated";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sessions = await _sessionService.GetAllAsync();
        await Send.OkAsync(sessions, ct);
    }
}
