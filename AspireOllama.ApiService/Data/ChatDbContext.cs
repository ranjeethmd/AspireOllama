using AspireOllama.Shared;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AspireOllama.ApiService.Data;

public class ChatDbContext : DbContext
{
    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options)
    {
    }

    public DbSet<ChatSessionEntity> ChatSessions => Set<ChatSessionEntity>();
    public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChatSessionEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(500);
        });

        modelBuilder.Entity<ChatMessageEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Role).HasMaxLength(50);
            entity.Property(e => e.ImagesJson).HasColumnType("TEXT");
            entity.Property(e => e.FilesJson).HasColumnType("TEXT");
            entity.HasOne<ChatSessionEntity>()
                .WithMany()
                .HasForeignKey(e => e.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

public class ChatSessionEntity
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ChatMessageEntity
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ImagesJson { get; set; } = "[]";
    public string FilesJson { get; set; } = "[]";
    public DateTime Timestamp { get; set; }

    public List<ImageAttachmentData> GetImages()
    {
        if (string.IsNullOrEmpty(ImagesJson))
            return new List<ImageAttachmentData>();
        return JsonSerializer.Deserialize<List<ImageAttachmentData>>(ImagesJson) ?? new List<ImageAttachmentData>();
    }

    public void SetImages(List<ImageAttachmentData> images)
    {
        ImagesJson = JsonSerializer.Serialize(images);
    }

    public List<FileAttachmentData> GetFiles()
    {
        if (string.IsNullOrEmpty(FilesJson))
            return new List<FileAttachmentData>();
        return JsonSerializer.Deserialize<List<FileAttachmentData>>(FilesJson) ?? new List<FileAttachmentData>();
    }

    public void SetFiles(List<FileAttachmentData> files)
    {
        FilesJson = JsonSerializer.Serialize(files);
    }
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
