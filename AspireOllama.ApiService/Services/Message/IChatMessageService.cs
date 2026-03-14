using AspireOllama.Shared;

namespace AspireOllama.ApiService.Services.Message;

/// <summary>
/// Interface for chat message operations.
/// Single responsibility: CRUD operations for chat messages only.
/// </summary>
public interface IChatMessageService
{
    /// <summary>
    /// Adds a message to a chat session.
    /// </summary>
    /// <param name="sessionId">Target session ID</param>
    /// <param name="role">"user" or "assistant"</param>
    /// <param name="content">Text content of the message</param>
    /// <param name="images">Optional image attachments</param>
    /// <param name="files">Optional file attachments</param>
    Task AddAsync(string sessionId, string role, string content,
        List<ImageAttachment>? images = null, List<FileAttachment>? files = null);

    /// <summary>
    /// Retrieves all messages for a session in chronological order.
    /// </summary>
    Task<List<ChatHistoryMessage>> GetBySessionIdAsync(string sessionId);

    /// <summary>
    /// Gets the count of messages in a session.
    /// </summary>
    Task<int> GetCountAsync(string sessionId);
}
