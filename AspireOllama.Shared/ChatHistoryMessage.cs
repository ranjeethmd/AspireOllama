namespace AspireOllama.Shared;

public class ChatHistoryMessage
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public List<ImageAttachment> Images { get; set; } = new();
    public List<FileAttachment> Files { get; set; } = new();
    public DateTime Timestamp { get; set; }
}
