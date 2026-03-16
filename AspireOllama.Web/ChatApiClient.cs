using AspireOllama.Shared;

namespace AspireOllama.Web;

/// <summary>
/// HTTP client for communicating with the AspireOllama API service.
/// Handles chat messages, sessions, and history operations.
/// </summary>
/// <param name="httpClient">Configured HttpClient with base URL pointing to API service</param>
public class ChatApiClient(HttpClient httpClient)
{
    public async Task<ChatMessageResponse> SendMessageAsync(ChatMessageRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/chat", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatMessageResponse>(cancellationToken)
            ?? new ChatMessageResponse();
    }

    /// <summary>
    /// Creates a new chat session.
    /// </summary>
    /// <returns>The newly created session with ID and default title</returns>
    public async Task<ChatSession> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync("/sessions", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatSession>(cancellationToken)
            ?? new ChatSession();
    }

    /// <summary>
    /// Retrieves all chat sessions, ordered by most recently updated.
    /// </summary>
    /// <returns>List of sessions for sidebar display</returns>
    public async Task<List<ChatSession>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<ChatSession>>("/sessions", cancellationToken)
            ?? new List<ChatSession>();
    }

    /// <summary>
    /// Retrieves a session with all its messages for display.
    /// </summary>
    /// <param name="sessionId">The session ID to retrieve</param>
    /// <returns>Session with messages, or null if not found</returns>
    public async Task<ChatSessionDetails?> GetSessionHistoryAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/sessions/{sessionId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<ChatSessionDetails>(cancellationToken);
    }

    /// <summary>
    /// Deletes a chat session and all its messages.
    /// </summary>
    /// <param name="sessionId">The session ID to delete</param>
    /// <returns>True if deleted successfully</returns>
    public async Task<bool> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/sessions/{sessionId}", cancellationToken);
        return response.IsSuccessStatusCode;
    }

    // ============================================================
    // A2A Agent Operations
    // ============================================================

    /// <summary>
    /// Gets all available A2A agents and their tools.
    /// </summary>
    public async Task<List<AgentInfo>> GetAgentsAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<List<AgentInfo>>("/agents", cancellationToken)
            ?? [];
    }

    /// <summary>
    /// Calls a specific tool on an A2A agent.
    /// </summary>
    public async Task<AgentCallResponse> CallAgentToolAsync(AgentCallRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/agents/call", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentCallResponse>(cancellationToken)
            ?? new AgentCallResponse();
    }

    /// <summary>
    /// Runs a multi-agent workflow for a task.
    /// </summary>
    public async Task<AgentWorkflowResponse> RunWorkflowAsync(AgentWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/agents/workflow", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentWorkflowResponse>(cancellationToken)
            ?? new AgentWorkflowResponse();
    }
}
