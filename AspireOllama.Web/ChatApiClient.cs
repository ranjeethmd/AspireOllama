using System.Security.Claims;
using AspireOllama.Shared;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Identity.Abstractions;

namespace AspireOllama.Web;

/// <summary>
/// HTTP client for communicating with the AspireOllama API service.
/// Uses IDownstreamApi from Microsoft.Identity.Web to acquire OBO tokens.
/// Resolves ClaimsPrincipal via AuthenticationStateProvider to support
/// Blazor Server circuits where HttpContext is not available.
/// </summary>
public class ChatApiClient(IDownstreamApi downstreamApi, AuthenticationStateProvider authStateProvider)
{
    private const string ApiName = "ApiService";

    private async Task<ClaimsPrincipal> GetUserAsync()
    {
        var authState = await authStateProvider.GetAuthenticationStateAsync();
        return authState.User;
    }

    public async Task<ChatMessageResponse> SendMessageAsync(ChatMessageRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync();
        return await downstreamApi.PostForUserAsync<ChatMessageRequest, ChatMessageResponse>(
            ApiName, request,
            options => options.RelativePath = "api/chat",
            user,
            cancellationToken: cancellationToken)
            ?? new ChatMessageResponse();
    }

    public async Task<ChatSession> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync();
        var response = await downstreamApi.CallApiForUserAsync(
            ApiName,
            options =>
            {
                options.HttpMethod = HttpMethod.Post.ToString();
                options.RelativePath = "api/sessions";
            },
            user,
            cancellationToken: cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ChatSession>(cancellationToken)
            ?? new ChatSession();
    }

    public async Task<List<ChatSession>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync();
        return await downstreamApi.GetForUserAsync<List<ChatSession>>(
            ApiName,
            options => options.RelativePath = "api/sessions",
            user,
            cancellationToken: cancellationToken)
            ?? [];
    }

    public async Task<ChatSessionDetails?> GetSessionHistoryAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync();
        return await downstreamApi.GetForUserAsync<ChatSessionDetails>(
            ApiName,
            options => options.RelativePath = $"api/sessions/{sessionId}",
            user,
            cancellationToken: cancellationToken);
    }

    public async Task<bool> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync();
        var response = await downstreamApi.CallApiForUserAsync(
            ApiName,
            options =>
            {
                options.HttpMethod = HttpMethod.Delete.ToString();
                options.RelativePath = $"api/sessions/{sessionId}";
            },
            user,
            cancellationToken: cancellationToken);
        return response.IsSuccessStatusCode;
    }

    // ============================================================
    // A2A Agent Operations
    // ============================================================

    public async Task<List<AgentInfo>> GetAgentsAsync(CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync();
        return await downstreamApi.GetForUserAsync<List<AgentInfo>>(
            ApiName,
            options => options.RelativePath = "api/agents",
            user,
            cancellationToken: cancellationToken)
            ?? [];
    }

    public async Task<AgentCallResponse> CallAgentToolAsync(AgentCallRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync();
        return await downstreamApi.PostForUserAsync<AgentCallRequest, AgentCallResponse>(
            ApiName, request,
            options => options.RelativePath = "api/agents/call",
            user,
            cancellationToken: cancellationToken)
            ?? new AgentCallResponse();
    }

    public async Task<AgentWorkflowResponse> RunWorkflowAsync(AgentWorkflowRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync();
        return await downstreamApi.PostForUserAsync<AgentWorkflowRequest, AgentWorkflowResponse>(
            ApiName, request,
            options => options.RelativePath = "api/agents/workflow",
            user,
            cancellationToken: cancellationToken)
            ?? new AgentWorkflowResponse();
    }
}
