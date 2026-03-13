using AspireOllama.ApiService.Data;
using AspireOllama.Shared;
using Microsoft.EntityFrameworkCore;

namespace AspireOllama.ApiService.Services;

public class ChatHistoryService
{
    private readonly ChatDbContext _context;

    public ChatHistoryService(ChatDbContext context)
    {
        _context = context;
    }

    public async Task<ChatSession> CreateSessionAsync()
    {
        var session = new ChatSessionEntity
        {
            Id = Guid.NewGuid().ToString(),
            Title = "New Chat",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ChatSessions.Add(session);
        await _context.SaveChangesAsync();

        return ToDto(session);
    }

    public async Task<List<ChatSession>> GetSessionsAsync()
    {
        var sessions = await _context.ChatSessions
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();

        return sessions.Select(ToDto).ToList();
    }

    public async Task<ChatSessionDetails?> GetSessionWithMessagesAsync(string sessionId)
    {
        var session = await _context.ChatSessions.FindAsync(sessionId);
        if (session == null)
            return null;

        var messages = await _context.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.Timestamp)
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

    public async Task<bool> DeleteSessionAsync(string sessionId)
    {
        var session = await _context.ChatSessions.FindAsync(sessionId);
        if (session == null)
            return false;

        _context.ChatSessions.Remove(session);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task AddMessageAsync(string sessionId, string role, string content, List<ImageAttachment>? images = null)
    {
        var message = new ChatMessageEntity
        {
            SessionId = sessionId,
            Role = role,
            Content = content,
            Timestamp = DateTime.UtcNow
        };

        if (images != null && images.Count > 0)
        {
            message.SetImages(images.Select(i => new ImageAttachmentData
            {
                FileName = i.FileName,
                ContentType = i.ContentType,
                Base64Data = i.Base64Data
            }).ToList());
        }

        _context.ChatMessages.Add(message);

        // Update session title from first user message
        if (role == "user")
        {
            var session = await _context.ChatSessions.FindAsync(sessionId);
            if (session != null)
            {
                var messageCount = await _context.ChatMessages.CountAsync(m => m.SessionId == sessionId);
                if (messageCount == 0) // This is the first message
                {
                    session.Title = content.Length > 50 ? content.Substring(0, 47) + "..." : content;
                }
                session.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<List<ChatHistoryMessage>> GetMessagesAsync(string sessionId)
    {
        var messages = await _context.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        return messages.Select(ToMessageDto).ToList();
    }

    private static ChatSession ToDto(ChatSessionEntity entity) => new()
    {
        Id = entity.Id,
        Title = entity.Title,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static ChatHistoryMessage ToMessageDto(ChatMessageEntity entity)
    {
        var images = entity.GetImages();
        return new ChatHistoryMessage
        {
            Id = entity.Id,
            SessionId = entity.SessionId,
            Role = entity.Role,
            Content = entity.Content,
            Images = images.Select(i => new ImageAttachment
            {
                FileName = i.FileName,
                ContentType = i.ContentType,
                Base64Data = i.Base64Data
            }).ToList(),
            Timestamp = entity.Timestamp
        };
    }
}
