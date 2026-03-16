using System.Text.Json.Serialization;

namespace AspireOllama.A2A.Shared;

/// <summary>
/// Task object following A2A Protocol specification.
/// Represents a unit of work with lifecycle management.
/// </summary>
public class A2ATask
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("contextId")]
    public string ContextId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("status")]
    public A2ATaskStatus Status { get; set; } = new();

    [JsonPropertyName("artifacts")]
    public List<A2AArtifact> Artifacts { get; set; } = [];

    [JsonPropertyName("history")]
    public List<A2AMessage> History { get; set; } = [];

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

public class A2ATaskStatus
{
    [JsonPropertyName("state")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public TaskState State { get; set; } = TaskState.Submitted;

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("progress")]
    public int? Progress { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TaskState
{
    Submitted,
    Working,
    Completed,
    Failed,
    Canceled,
    InputRequired,
    AuthRequired
}

public class A2AArtifact
{
    [JsonPropertyName("artifactId")]
    public string ArtifactId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("parts")]
    public List<A2APart> Parts { get; set; } = [];

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
