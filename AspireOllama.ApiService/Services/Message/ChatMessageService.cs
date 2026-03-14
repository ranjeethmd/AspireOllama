using AspireOllama.ApiService.Data;
using AspireOllama.Shared;
using Microsoft.EntityFrameworkCore;

namespace AspireOllama.ApiService.Services.Message;

/// <summary>
/// Manages chat message persistence in the database.
/// Single responsibility: CRUD operations for chat messages only.
/// </summary>
public class ChatMessageService : IChatMessageService
{
    private readonly ChatDbContext _context;

    public ChatMessageService(ChatDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(string sessionId, string role, string content,
        List<ImageAttachment>? images = null, List<FileAttachment>? files = null)
    {
        var message = new ChatMessageEntity
        {
            SessionId = sessionId,
            Role = role,
            Content = content,
            Timestamp = DateTime.UtcNow
        };

        // Serialize image attachments to JSON for storage
        if (images != null && images.Count > 0)
        {
            message.SetImages(images.Select(i => new ImageAttachmentData
            {
                FileName = i.FileName,
                ContentType = i.ContentType,
                Base64Data = i.Base64Data
            }).ToList());
        }

        // Serialize file attachments to JSON for storage
        if (files != null && files.Count > 0)
        {
            message.SetFiles(files.Select(f => new FileAttachmentData
            {
                FileName = f.FileName,
                ContentType = f.ContentType,
                Base64Data = f.Base64Data,
                Type = f.Type
            }).ToList());
        }

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<List<ChatHistoryMessage>> GetBySessionIdAsync(string sessionId)
    {
        var messages = await _context.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();

        return messages.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<int> GetCountAsync(string sessionId)
    {
        return await _context.ChatMessages.CountAsync(m => m.SessionId == sessionId);
    }

    // ============================================================
    // DTO Conversion
    // ============================================================

    private static ChatHistoryMessage ToDto(ChatMessageEntity entity)
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
