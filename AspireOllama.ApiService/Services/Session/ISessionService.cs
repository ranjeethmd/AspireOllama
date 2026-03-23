using AspireOllama.Shared;

namespace AspireOllama.ApiService.Services.Session;

public interface ISessionService
{
    Task<ChatSession> CreateAsync(string userId, string userName);
    Task<List<ChatSession>> GetAllAsync(string userId);
    Task<ChatSessionDetails?> GetByIdAsync(string sessionId, string userId);
    Task<bool> DeleteAsync(string sessionId, string userId);
    Task UpdateTitleAsync(string sessionId, string userId, string title);
    Task TouchAsync(string sessionId, string userId);
}
