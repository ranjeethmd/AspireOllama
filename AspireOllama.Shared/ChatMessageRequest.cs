namespace AspireOllama.Shared;

public class ChatMessageRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<ImageAttachment> Images { get; set; } = new();
    public List<FileAttachment> Files { get; set; } = new();
}
