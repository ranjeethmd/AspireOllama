using AspireOllama.Shared;

namespace AspireOllama.ApiService.Services.Session;

public interface ISessionService
{
    Task<ChatSession> CreateAsync(string userId, string userName);
    Task<List<ChatSession>> GetAllAsync(string userId);
    Task<ChatSessionDetails?> GetByIdAsync(string sessionId);
    Task<bool> DeleteAsync(string sessionId);
    Task UpdateTitleAsync(string sessionId, string title);
    Task TouchAsync(string sessionId);
}
