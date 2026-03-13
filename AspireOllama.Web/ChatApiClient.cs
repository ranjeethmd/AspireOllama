using AspireOllama.Shared;

namespace AspireOllama.Web;

public class ChatApiClient(HttpClient httpClient)
{
    public async Task<string> SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/chat", message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<ChatMessageResponse> SendMessageAsync(ChatMessageRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/chat", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatMessageResponse>(cancellationToken)
            ?? new ChatMessageResponse();
    }

    public async Task<ChatSession> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync("/sessions", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatSession>(cancellationToken)
            ?? new ChatSession();
    }

    public async Task<List<ChatSession>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<ChatSession>>("/sessions", cancellationToken)
            ?? new List<ChatSession>();
    }

    public async Task<ChatSessionDetails?> GetSessionHistoryAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/sessions/{sessionId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<ChatSessionDetails>(cancellationToken);
    }

    public async Task<bool> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/sessions/{sessionId}", cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
