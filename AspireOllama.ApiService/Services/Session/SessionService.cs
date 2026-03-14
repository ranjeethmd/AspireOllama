using AspireOllama.ApiService.Data;
using AspireOllama.Shared;
using Microsoft.EntityFrameworkCore;

namespace AspireOllama.ApiService.Services.Session;

/// <summary>
/// Manages chat session persistence in the database.
/// Single responsibility: CRUD operations for chat sessions only.
/// </summary>
public class SessionService : ISessionService
{
    private readonly ChatDbContext _context;

    public SessionService(ChatDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<ChatSession> CreateAsync()
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

    /// <inheritdoc />
    public async Task<List<ChatSession>> GetAllAsync()
    {
        var sessions = await _context.ChatSessions
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();

        return sessions.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<ChatSessionDetails?> GetByIdAsync(string sessionId)
    {
        var session = await _context.ChatSessions.FindAsync(sessionId);
        if (session is null)
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

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string sessionId)
    {
        var session = await _context.ChatSessions.FindAsync(sessionId);
        if (session is null)
            return false;

        _context.ChatSessions.Remove(session);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc />
    public async Task UpdateTitleAsync(string sessionId, string title)
    {
        var session = await _context.ChatSessions.FindAsync(sessionId);
        if (session is null)
            return;

        // Truncate long titles
        session.Title = title.Length > 50 ? title.Substring(0, 47) + "..." : title;
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task TouchAsync(string sessionId)
    {
        var session = await _context.ChatSessions.FindAsync(sessionId);
        if (session is null)
            return;

        session.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    // ============================================================
    // DTO Conversion Methods
    // ============================================================

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
        var files = entity.GetFiles();

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
            Files = files.Select(f => new FileAttachment
            {
                FileName = f.FileName,
                ContentType = f.ContentType,
                Base64Data = f.Base64Data,
                Type = f.Type
            }).ToList(),
            Timestamp = entity.Timestamp
        };
    }
}
