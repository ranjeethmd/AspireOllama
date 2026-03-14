using AspireOllama.ApiService.Services.Session;
using AspireOllama.Shared;
using FastEndpoints;

namespace AspireOllama.ApiService.Endpoints;

/// <summary>
/// Request DTO for getting a specific session.
/// </summary>
public class GetSessionRequest
{
    public string Id { get; set; } = string.Empty;
}

/// <summary>
/// Retrieves a chat session with all its messages.
/// </summary>
public class GetSessionEndpoint : Endpoint<GetSessionRequest, ChatSessionDetails>
{
    private readonly ISessionService _sessionService;

    public GetSessionEndpoint(ISessionService sessionService)
    {
        _sessionService = sessionService;
    }

    public override void Configure()
    {
        Get("/sessions/{Id}");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get a chat session with messages";
            s.Description = "Returns a chat session with its full message history";
        });
    }

    public override async Task HandleAsync(GetSessionRequest req, CancellationToken ct)
    {
        var session = await _sessionService.GetByIdAsync(req.Id);

        if (session is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(session, ct);
    }
}
