namespace AspireOllama.Shared;

public class ChatSessionDetails
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ChatHistoryMessage> Messages { get; set; } = new();
}
