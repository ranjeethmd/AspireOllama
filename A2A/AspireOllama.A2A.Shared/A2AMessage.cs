using System.Text.Json.Serialization;

namespace AspireOllama.A2A.Shared;

/// <summary>
/// Message format following A2A Protocol specification.
/// </summary>
public class A2AMessage
{
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("role")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MessageRole Role { get; set; } = MessageRole.User;

    [JsonPropertyName("parts")]
    public List<A2APart> Parts { get; set; } = [];

    [JsonPropertyName("taskId")]
    public string? TaskId { get; set; }

    [JsonPropertyName("contextId")]
    public string? ContextId { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageRole
{
    User,
    Agent
}

/// <summary>
/// Content part of a message or artifact.
/// </summary>
public class A2APart
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

    public static A2APart FromText(string text) => new() { Text = text };
    public static A2APart FromData(object data, string mediaType = "application/json") => new() { Data = data, MediaType = mediaType };
}

/// <summary>
/// Request to send a message to an agent.
/// </summary>
public class SendMessageRequest
{
    [JsonPropertyName("message")]
    public A2AMessage Message { get; set; } = new();

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
/// Response from sending a message to an agent.
/// </summary>
public class SendMessageResponse
{
    [JsonPropertyName("task")]
    public A2ATask? Task { get; set; }

    [JsonPropertyName("message")]
    public A2AMessage? Message { get; set; }
}
