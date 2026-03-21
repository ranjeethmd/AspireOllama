namespace AspireOllama.A2A.Shared;

/// <summary>
/// Push notification webhook configuration for A2A task updates.
/// </summary>
public class PushNotificationConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TaskId { get; set; } = "";
    public string WebhookUrl { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
