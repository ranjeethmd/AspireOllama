using AspireOllama.ApiService.Data;
using AspireOllama.Shared;
using MongoDB.Driver;

namespace AspireOllama.ApiService.Services.Session;

public class SessionService(MongoCollections collections) : ISessionService
{
    public async Task<ChatSession> CreateAsync()
    {
        var session = new ChatSessionDocument
        {
            Id = Guid.NewGuid().ToString(),
            Title = "New Chat",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await collections.Sessions.InsertOneAsync(session);
        return ToDto(session);
    }

    public async Task<List<ChatSession>> GetAllAsync()
    {
        var sessions = await collections.Sessions
            .Find(FilterDefinition<ChatSessionDocument>.Empty)
            .SortByDescending(s => s.UpdatedAt)
            .ToListAsync();

        return sessions.Select(ToDto).ToList();
    }

    public async Task<ChatSessionDetails?> GetByIdAsync(string sessionId)
    {
        var session = await collections.Sessions
            .Find(s => s.Id == sessionId)
            .FirstOrDefaultAsync();

        if (session is null)
            return null;

        var messages = await collections.Messages
            .Find(m => m.SessionId == sessionId)
            .SortBy(m => m.Timestamp)
            .ToListAsync();

        return new ChatSessionDetails
        {
            Id = session.Id,
            Title = session.Title,
            CreatedAt = session.CreatedAt,
            UpdatedAt = session.UpdatedAt,
            Messages = messages.Select(ToMessageDto).ToList()
        };
    }

    public async Task<bool> DeleteAsync(string sessionId)
    {
        var result = await collections.Sessions.DeleteOneAsync(s => s.Id == sessionId);
        if (result.DeletedCount == 0)
            return false;

        await collections.Messages.DeleteManyAsync(m => m.SessionId == sessionId);
        return true;
    }

    public async Task UpdateTitleAsync(string sessionId, string title)
    {
        var truncated = title.Length > 50 ? title[..47] + "..." : title;
        await collections.Sessions.UpdateOneAsync(
            s => s.Id == sessionId,
            Builders<ChatSessionDocument>.Update.Set(s => s.Title, truncated));
    }

    public async Task TouchAsync(string sessionId)
    {
        await collections.Sessions.UpdateOneAsync(
            s => s.Id == sessionId,
            Builders<ChatSessionDocument>.Update.Set(s => s.UpdatedAt, DateTime.UtcNow));
    }

    private static ChatSession ToDto(ChatSessionDocument doc) => new()
    {
        Id = doc.Id,
        Title = doc.Title,
        CreatedAt = doc.CreatedAt,
        UpdatedAt = doc.UpdatedAt
    };

    private static ChatHistoryMessage ToMessageDto(ChatMessageDocument doc) => new()
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
