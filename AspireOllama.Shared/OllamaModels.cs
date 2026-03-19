namespace AspireOllama.Shared;

/// <summary>
/// Central registry of Ollama model names and Aspire resource names.
/// Change model tags here to switch models across the entire application.
/// </summary>
public static class OllamaModels
{
    // ── Ollama model tags (pulled by Ollama) ──
    public const string ChatModel = "qwen3:32b";
    public const string VisionModel = "qwen2.5vl:32b";
    public const string EmbeddingModel = "nomic-embed-text";

    // ── Aspire resource names (used in connection strings) ──
    public const string ChatResource = "qwen-chat";
    public const string VisionResource = "qwen-vision";
    public const string EmbeddingResource = "embedding";

    // ── Keyed service names (DI registration) ──
    public const string ChatServiceKey = "chat";
    public const string VisionServiceKey = "vision";
}
