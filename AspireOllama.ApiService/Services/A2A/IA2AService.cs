using AspireOllama.Shared;

namespace AspireOllama.ApiService.Services.A2A;

public interface IA2AService
{
    Task<List<AgentInfo>> GetAgentsAsync(CancellationToken ct = default);
    Task<AgentCallResponse> CallAgentToolAsync(AgentCallRequest request, CancellationToken ct = default);
    Task<AgentWorkflowResponse> RunWorkflowAsync(AgentWorkflowRequest request, CancellationToken ct = default);
}
