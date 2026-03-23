using System.Text.Json;
using System.Text.Json.Serialization;

namespace AspireOllama.A2A.Protocol;

/// <summary>
/// Message per A2A Protocol specification.
/// </summary>
public class Message
{
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("role")]
    [JsonConverter(typeof(MessageRoleConverter))]
    public MessageRole Role { get; set; } = MessageRole.User;

    [JsonPropertyName("parts")]
    public List<Part> Parts { get; set; } = [];

    [JsonPropertyName("taskId")]
    public string? TaskId { get; set; }

    [JsonPropertyName("contextId")]
    public string? ContextId { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Message roles per A2A Protocol specification.
/// Serializes as lowercase: user, agent.
/// </summary>
public enum MessageRole
{
    User,
    Agent
}

/// <summary>
/// Custom JSON converter for MessageRole — serializes to A2A spec format (lowercase).
/// </summary>
public class MessageRoleConverter : JsonConverter<MessageRole>
{
    public override MessageRole Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "user" => MessageRole.User,
            "agent" => MessageRole.Agent,
            _ => MessageRole.User
        };

    public override void Write(Utf8JsonWriter writer, MessageRole value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            MessageRole.User => "user",
            MessageRole.Agent => "agent",
            _ => "user"
        });
}

/// <summary>
/// Content part per A2A Protocol specification.
/// </summary>
public class Part
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("raw")]
    public string? Raw { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("mediaType")]
    public string? MediaType { get; set; }

    [JsonPropertyName("filename")]
    public string? Filename { get; set; }

    public static Part FromText(string text) => new() { Text = text };
    public static Part FromData(object data, string mediaType = "application/json") => new() { Data = data, MediaType = mediaType };
}

/// <summary>
/// Request to send a message to an agent per A2A Protocol specification.
/// </summary>
public class SendMessageRequest
{
    [JsonPropertyName("message")]
    public Message Message { get; set; } = new();

    [JsonPropertyName("configuration")]
    public SendMessageConfiguration? Configuration { get; set; }
}

public class SendMessageConfiguration
{
    [JsonPropertyName("acceptedOutputModes")]
    public List<string>? AcceptedOutputModes { get; set; }

    [JsonPropertyName("returnImmediately")]
    public bool ReturnImmediately { get; set; } = false;

    [JsonPropertyName("blocking")]
    public bool Blocking { get; set; } = true;
}

/// <summary>
/// Response from sending a message to an agent per A2A Protocol specification.
/// </summary>
public class SendMessageResponse
{
    [JsonPropertyName("task")]
    public Task? Task { get; set; }

    [JsonPropertyName("message")]
    public Message? Message { get; set; }
}
