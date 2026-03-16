using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AspireOllama.A2A.Shared;

/// <summary>
/// Interface for A2A agent-to-agent communication.
/// </summary>
public interface IA2AAgentClient
{
    Task<AgentCard?> GetAgentCardAsync(string agentName, CancellationToken ct = default);
    Task<SendMessageResponse> SendMessageAsync(string agentName, SendMessageRequest request, CancellationToken ct = default);
    Task<SendMessageResponse> SendTextAsync(string agentName, string text, CancellationToken ct = default);
    Task<A2ATask?> GetTaskAsync(string agentName, string taskId, CancellationToken ct = default);
    Task<bool> CancelTaskAsync(string agentName, string taskId, CancellationToken ct = default);
    IEnumerable<string> GetKnownAgents();
}

/// <summary>
/// Configuration for known agents.
/// </summary>
public class KnownAgentsOptions
{
    public const string SectionName = "A2A:KnownAgents";
    public Dictionary<string, string> Agents { get; set; } = new();
}

/// <summary>
/// HTTP-based A2A client for inter-agent communication.
/// </summary>
public class A2AAgentClient : IA2AAgentClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<A2AAgentClient> _logger;
    private readonly KnownAgentsOptions _knownAgents;
    private readonly JsonSerializerOptions _jsonOptions;

    public A2AAgentClient(
        IHttpClientFactory httpClientFactory,
        IOptions<KnownAgentsOptions> knownAgents,
        ILogger<A2AAgentClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _knownAgents = knownAgents.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public IEnumerable<string> GetKnownAgents() => _knownAgents.Agents.Keys;

    public async Task<AgentCard?> GetAgentCardAsync(string agentName, CancellationToken ct = default)
    {
        try
        {
            var client = GetHttpClient(agentName);
            var response = await client.GetAsync("/.well-known/agent.json", ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get agent card from {Agent}: {Status}", agentName, response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<AgentCard>(_jsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting agent card from {Agent}", agentName);
            return null;
        }
    }

    public async Task<SendMessageResponse> SendMessageAsync(string agentName, SendMessageRequest request, CancellationToken ct = default)
    {
        try
        {
            var client = GetHttpClient(agentName);
            _logger.LogInformation("Sending A2A message to {Agent}", agentName);

            var response = await client.PostAsJsonAsync("/a2a/message:send", request, _jsonOptions, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SendMessageResponse>(_jsonOptions, ct);
            return result ?? new SendMessageResponse();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to {Agent}", agentName);
            return new SendMessageResponse
            {
                Task = new A2ATask
                {
                    Status = new A2ATaskStatus
                    {
                        State = TaskState.Failed,
                        Message = $"Failed to communicate with {agentName}: {ex.Message}"
                    }
                }
            };
        }
    }

    public async Task<SendMessageResponse> SendTextAsync(string agentName, string text, CancellationToken ct = default)
    {
        return await SendMessageAsync(agentName, new SendMessageRequest
        {
            Message = new A2AMessage
            {
                Role = MessageRole.User,
                Parts = [A2APart.FromText(text)]
            }
        }, ct);
    }

    public async Task<A2ATask?> GetTaskAsync(string agentName, string taskId, CancellationToken ct = default)
    {
        try
        {
            var client = GetHttpClient(agentName);
            var response = await client.GetAsync($"/a2a/tasks/{taskId}", ct);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<A2ATask>(_jsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task {TaskId} from {Agent}", taskId, agentName);
            return null;
        }
    }

    public async Task<bool> CancelTaskAsync(string agentName, string taskId, CancellationToken ct = default)
    {
        try
        {
            var client = GetHttpClient(agentName);
            var response = await client.PostAsync($"/a2a/tasks/{taskId}:cancel", null, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling task {TaskId} on {Agent}", taskId, agentName);
            return false;
        }
    }

    private HttpClient GetHttpClient(string agentName)
    {
        if (_knownAgents.Agents.TryGetValue(agentName, out var url))
        {
            var client = _httpClientFactory.CreateClient(agentName);
            if (client.BaseAddress == null)
            {
                client.BaseAddress = new Uri(url.TrimEnd('/'));
            }
            return client;
        }

        // Try to use agent name directly as HTTP client name (Aspire service discovery)
        return _httpClientFactory.CreateClient(agentName);
    }
}
