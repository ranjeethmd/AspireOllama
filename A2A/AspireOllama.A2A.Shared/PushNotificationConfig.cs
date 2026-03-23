namespace AspireOllama.A2A.Protocol;

/// <summary>
/// Push notification webhook configuration per A2A Protocol specification.
/// </summary>
public class PushNotificationConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TaskId { get; set; } = "";
    public string WebhookUrl { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
