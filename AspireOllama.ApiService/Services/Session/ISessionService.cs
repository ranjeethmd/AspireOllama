using AspireOllama.Shared;

namespace AspireOllama.ApiService.Services.Session;

/// <summary>
/// Interface for session management operations.
/// Single responsibility: CRUD operations for chat sessions.
/// </summary>
public interface ISessionService
{
    /// <summary>
    /// Creates a new chat session with a default title.
    /// </summary>
    Task<ChatSession> CreateAsync();

    /// <summary>
    /// Retrieves all sessions ordered by most recently updated.
    /// </summary>
    Task<List<ChatSession>> GetAllAsync();

    /// <summary>
    /// Retrieves a session with all its messages.
    /// </summary>
    Task<ChatSessionDetails?> GetByIdAsync(string sessionId);

    /// <summary>
    /// Deletes a session and all its messages.
    /// </summary>
    Task<bool> DeleteAsync(string sessionId);

    /// <summary>
    /// Updates the session title.
    /// </summary>
    Task UpdateTitleAsync(string sessionId, string title);

    /// <summary>
    /// Updates the session's last updated timestamp.
    /// </summary>
    Task TouchAsync(string sessionId);
}
