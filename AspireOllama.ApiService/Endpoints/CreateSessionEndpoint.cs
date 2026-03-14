using AspireOllama.ApiService.Services.Session;
using AspireOllama.Shared;
using FastEndpoints;

namespace AspireOllama.ApiService.Endpoints;

/// <summary>
/// Creates a new chat session.
/// </summary>
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
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Create a new chat session";
            s.Description = "Creates a new chat session and returns the session details";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var session = await _sessionService.CreateAsync();
        await Send.OkAsync(session, ct);
    }
}
