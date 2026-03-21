using FastEndpoints;
using Microsoft.Extensions.AI;
using static AspireOllama.ServiceDefaults.Authentication.AuthRoles;

namespace AspireOllama.ApiService.Endpoints;

public class TestOllamaResponse
{
    public bool Success { get; set; }
    public string? Response { get; set; }
    public string? Error { get; set; }
}

public class TestOllamaEndpoint([FromKeyedServices(AspireOllama.Shared.OllamaModels.ChatServiceKey)] IChatClient chatClient)
    : EndpointWithoutRequest<TestOllamaResponse>
{
    public override void Configure()
    {
        Get("/test-ollama");
        Roles(ApiChatRead);
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        try
        {
            var response = await chatClient.GetResponseAsync("Say hello in one word", cancellationToken: ct);
            await Send.OkAsync(new TestOllamaResponse { Success = true, Response = response.Text }, ct);
        }
        catch (Exception ex)
        {
            await Send.OkAsync(new TestOllamaResponse { Success = false, Error = ex.Message }, ct);
        }
    }
}
