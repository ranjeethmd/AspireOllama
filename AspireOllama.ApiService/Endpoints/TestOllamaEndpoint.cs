using FastEndpoints;
using Microsoft.Extensions.AI;

namespace AspireOllama.ApiService.Endpoints;

public class TestOllamaResponse
{
    public bool Success { get; set; }
    public string? Response { get; set; }
    public string? Error { get; set; }
}

public class TestOllamaEndpoint : EndpointWithoutRequest<TestOllamaResponse>
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<TestOllamaEndpoint> _logger;

    public TestOllamaEndpoint(IChatClient chatClient, ILogger<TestOllamaEndpoint> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/test-ollama");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Test Ollama connection";
            s.Description = "Tests if Ollama is running and responding correctly";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Testing Ollama connection...");
            var response = await _chatClient.GetResponseAsync("Say hello in one word", cancellationToken: ct);

            await Send.OkAsync(new TestOllamaResponse
            {
                Success = true,
                Response = response.Text
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama test failed");
            await Send.OkAsync(new TestOllamaResponse
            {
                Success = false,
                Error = ex.Message
            }, ct);
        }
    }
}
