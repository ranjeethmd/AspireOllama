using System.Text.Json;
using System.Text.Json.Serialization;

namespace AspireOllama.A2A.Protocol;

/// <summary>
/// Task per A2A Protocol specification.
/// Use fully qualified Protocol.Task to avoid conflict with System.Threading.Tasks.Task.
/// </summary>
public class Task
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("contextId")]
    public string ContextId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("status")]
    public TaskStatus Status { get; set; } = new();

    [JsonPropertyName("artifacts")]
    public List<Artifact> Artifacts { get; set; } = [];

    [JsonPropertyName("history")]
    public List<Message> History { get; set; } = [];

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("completedAt")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// TaskStatus per A2A Protocol specification.
/// </summary>
public class TaskStatus
{
    [JsonPropertyName("state")]
    [JsonConverter(typeof(TaskStateConverter))]
    public TaskState State { get; set; } = TaskState.Submitted;

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("progress")]
    public int? Progress { get; set; }
}

/// <summary>
/// Task states per A2A Protocol specification.
/// Serializes as lowercase/kebab-case: submitted, working, completed, failed, canceled, input-required, auth-required.
/// </summary>
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

/// <summary>
/// Custom JSON converter for TaskState — serializes to A2A spec format (lowercase/kebab-case).
/// </summary>
public class TaskStateConverter : JsonConverter<TaskState>
{
    public override TaskState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString() switch
        {
            "submitted" => TaskState.Submitted,
            "working" => TaskState.Working,
            "completed" => TaskState.Completed,
            "failed" => TaskState.Failed,
            "canceled" => TaskState.Canceled,
            "input-required" => TaskState.InputRequired,
            "auth-required" => TaskState.AuthRequired,
            _ => TaskState.Submitted
        };

    public override void Write(Utf8JsonWriter writer, TaskState value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value switch
        {
            TaskState.Submitted => "submitted",
            TaskState.Working => "working",
            TaskState.Completed => "completed",
            TaskState.Failed => "failed",
            TaskState.Canceled => "canceled",
            TaskState.InputRequired => "input-required",
            TaskState.AuthRequired => "auth-required",
            _ => "submitted"
        });
}

/// <summary>
/// Artifact per A2A Protocol specification.
/// </summary>
public class Artifact
{
    [JsonPropertyName("artifactId")]
    public string ArtifactId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("parts")]
    public List<Part> Parts { get; set; } = [];

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
