using AspireOllama.Shared;

namespace AspireOllama.ApiService.Services.AI;

/// <summary>
/// Interface for AI chat operations.
/// Single responsibility: Building context and calling the AI model.
/// </summary>
public interface IAiChatService
{
    /// <summary>
    /// Processes a chat request: builds context from history and attachments,
    /// calls the AI model, and returns the response.
    /// </summary>
    /// <param name="request">The chat message request with content and attachments</param>
    /// <param name="history">Previous messages in the conversation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The AI response text</returns>
    Task<string> GetResponseAsync(
        ChatMessageRequest request,
        List<ChatHistoryMessage> history,
        CancellationToken cancellationToken = default);
}
