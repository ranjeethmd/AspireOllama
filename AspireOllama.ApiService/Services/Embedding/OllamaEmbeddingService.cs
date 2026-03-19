using Microsoft.Extensions.AI;

namespace AspireOllama.ApiService.Services.Embedding;

public class OllamaEmbeddingService(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    ILogger<OllamaEmbeddingService> logger) : IEmbeddingService
{
    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var results = await embeddingGenerator.GenerateAsync([text], cancellationToken: ct);
        return results[0].Vector.ToArray();
    }

    public async Task<List<float[]>> GetEmbeddingsAsync(List<string> texts, CancellationToken ct = default)
    {
        logger.LogInformation("Generating embeddings for {Count} texts", texts.Count);
        var results = await embeddingGenerator.GenerateAsync(texts, cancellationToken: ct);
        return results.Select(e => e.Vector.ToArray()).ToList();
    }
}
