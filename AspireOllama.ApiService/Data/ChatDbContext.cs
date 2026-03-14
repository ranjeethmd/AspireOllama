using AspireOllama.Shared;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AspireOllama.ApiService.Data;

/// <summary>
/// Entity Framework Core database context for chat persistence.
/// Uses SQLite to store chat sessions and messages with attachments.
/// </summary>
public class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
    {
    }

    /// <summary>Chat sessions (conversations)</summary>
    public DbSet<ChatSessionEntity> ChatSessions => Set<ChatSessionEntity>();

    /// <summary>Individual messages within sessions</summary>
    public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure ChatSession entity
        modelBuilder.Entity<ChatSessionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(500);
        });

        // Configure ChatMessage entity
        modelBuilder.Entity<ChatMessageEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).HasMaxLength(50);

            // Store attachments as JSON text (SQLite doesn't have native JSON type)
            entity.Property(e => e.ImagesJson).HasColumnType("TEXT");
            entity.Property(e => e.FilesJson).HasColumnType("TEXT");

            // Cascade delete: removing a session deletes all its messages
            entity.HasOne<ChatSessionEntity>()
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

// ============================================================
// Database Entities
// ============================================================

/// <summary>
/// Represents a chat session (conversation) in the database.
/// </summary>
public class ChatSessionEntity
{
    /// <summary>Unique session identifier (GUID string)</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display title (auto-generated from first message)</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>When the session was created</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When the session was last updated (new message added)</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Represents a single message in a chat session.
/// Attachments are stored as JSON for SQLite compatibility.
/// </summary>
public class ChatMessageEntity
{
    /// <summary>Auto-incrementing message ID</summary>
    public int Id { get; set; }

    /// <summary>Foreign key to parent session</summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>Message role: "user" or "assistant"</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Text content of the message</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>JSON-serialized image attachments</summary>
    public string ImagesJson { get; set; } = "[]";

    /// <summary>JSON-serialized document attachments</summary>
    public string FilesJson { get; set; } = "[]";

    /// <summary>When the message was sent</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Deserializes image attachments from JSON storage.
    /// </summary>
    public List<ImageAttachmentData> GetImages()
    {
        if (string.IsNullOrEmpty(ImagesJson))
            return new List<ImageAttachmentData>();
        return JsonSerializer.Deserialize<List<ImageAttachmentData>>(ImagesJson) ?? new List<ImageAttachmentData>();
    }

    /// <summary>
    /// Serializes image attachments to JSON for storage.
    /// </summary>
    public void SetImages(List<ImageAttachmentData> images)
    {
        ImagesJson = JsonSerializer.Serialize(images);
    }

    /// <summary>
    /// Deserializes document attachments from JSON storage.
    /// </summary>
    public List<FileAttachmentData> GetFiles()
    {
        if (string.IsNullOrEmpty(FilesJson))
            return new List<FileAttachmentData>();
        return JsonSerializer.Deserialize<List<FileAttachmentData>>(FilesJson) ?? new List<FileAttachmentData>();
    }

    /// <summary>
    /// Serializes document attachments to JSON for storage.
    /// </summary>
    public void SetFiles(List<FileAttachmentData> files)
    {
        FilesJson = JsonSerializer.Serialize(files);
    }
}

// ============================================================
// Attachment Data Models (for JSON serialization)
// ============================================================

/// <summary>
/// Image attachment data stored in database.
/// Contains base64-encoded image data for display and AI analysis.
/// </summary>
public class ImageAttachmentData
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Base64Data { get; set; } = string.Empty;
}

/// <summary>
/// Document attachment data stored in database.
/// Contains base64-encoded file data for text extraction on context rebuild.
/// </summary>
public class FileAttachmentData
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Base64Data { get; set; } = string.Empty;
    public FileType Type { get; set; } = FileType.Unknown;
}
