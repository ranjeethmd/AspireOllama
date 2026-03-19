using AspireOllama.ApiService.Data;
using AspireOllama.Shared;
using MongoDB.Driver;

namespace AspireOllama.ApiService.Services.Message;

public class ChatMessageService(MongoCollections collections) : IChatMessageService
{
    public async Task AddAsync(string sessionId, string role, string content,
        List<ImageAttachment>? images = null, List<FileAttachment>? files = null)
    {
        var message = new ChatMessageDocument
        {
            SessionId = sessionId,
            Role = role,
            Content = content,
            Timestamp = DateTime.UtcNow,
            Images = images?.Select(i => new ImageAttachmentData
            {
                FileName = i.FileName,
                ContentType = i.ContentType,
                Base64Data = i.Base64Data
            }).ToList() ?? [],
            Files = files?.Select(f => new FileAttachmentData
            {
                FileName = f.FileName,
                ContentType = f.ContentType,
                Base64Data = f.Base64Data,
                Type = f.Type
            }).ToList() ?? []
        };

        await collections.Messages.InsertOneAsync(message);
    }

    public async Task<List<ChatHistoryMessage>> GetBySessionIdAsync(string sessionId)
    {
        var messages = await collections.Messages
            .Find(m => m.SessionId == sessionId)
            .SortBy(m => m.Timestamp)
            .ToListAsync();

        return messages.Select(ToDto).ToList();
    }

    public async Task<int> GetCountAsync(string sessionId)
    {
        return (int)await collections.Messages.CountDocumentsAsync(m => m.SessionId == sessionId);
    }

    private static ChatHistoryMessage ToDto(ChatMessageDocument doc) => new()
    {
        Id = doc.Id,
        SessionId = doc.SessionId,
        Role = doc.Role,
        Content = doc.Content,
        Images = doc.Images.Select(i => new ImageAttachment
        {
            FileName = i.FileName,
            ContentType = i.ContentType,
            Base64Data = i.Base64Data
        }).ToList(),
        Files = doc.Files.Select(f => new FileAttachment
        {
            FileName = f.FileName,
            ContentType = f.ContentType,
            Base64Data = f.Base64Data,
            Type = f.Type
        }).ToList(),
        Timestamp = doc.Timestamp
    };
}
