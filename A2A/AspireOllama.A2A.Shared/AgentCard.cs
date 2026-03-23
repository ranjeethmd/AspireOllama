using System.Text.Json.Serialization;

namespace AspireOllama.A2A.Protocol;

/// <summary>
/// Agent Card per A2A Protocol specification.
/// Published at /.well-known/agent.json
/// </summary>
public class AgentCard
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("provider")]
    public AgentProvider? Provider { get; set; }

    [JsonPropertyName("capabilities")]
    public AgentCapabilities Capabilities { get; set; } = new();

    [JsonPropertyName("skills")]
    public List<AgentSkill> Skills { get; set; } = [];

    [JsonPropertyName("defaultInputModes")]
    public List<string> DefaultInputModes { get; set; } = ["text/plain"];

    [JsonPropertyName("defaultOutputModes")]
    public List<string> DefaultOutputModes { get; set; } = ["text/plain", "application/json"];

    [JsonPropertyName("documentationUrl")]
    public string? DocumentationUrl { get; set; }

    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }
}

public class AgentProvider
{
    [JsonPropertyName("organization")]
    public string Organization { get; set; } = "";

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public class AgentCapabilities
{
    [JsonPropertyName("streaming")]
    public bool Streaming { get; set; } = false;

    [JsonPropertyName("pushNotifications")]
    public bool PushNotifications { get; set; } = false;

    [JsonPropertyName("extendedAgentCard")]
    public bool ExtendedAgentCard { get; set; } = false;
}

public class AgentSkill
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("examples")]
    public List<string> Examples { get; set; } = [];

    [JsonPropertyName("inputModes")]
    public List<string>? InputModes { get; set; }

    [JsonPropertyName("outputModes")]
    public List<string>? OutputModes { get; set; }
}
