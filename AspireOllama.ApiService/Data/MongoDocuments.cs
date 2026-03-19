using AspireOllama.Shared;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace AspireOllama.ApiService.Data;

public class ChatSessionDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ChatMessageDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<ImageAttachmentData> Images { get; set; } = [];
    public List<FileAttachmentData> Files { get; set; } = [];
    public DateTime Timestamp { get; set; }
}

public class ImageAttachmentData
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Base64Data { get; set; } = string.Empty;
}

public class FileAttachmentData
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Base64Data { get; set; } = string.Empty;
    public FileType Type { get; set; } = FileType.Unknown;
}
