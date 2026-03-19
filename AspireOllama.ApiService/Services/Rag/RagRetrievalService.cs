using AspireOllama.ApiService.Services.Embedding;
using Qdrant.Client;

namespace AspireOllama.ApiService.Services.Rag;

/// <summary>
/// RAG retrieval using Qdrant vector database with dot product similarity.
/// Searches all documents globally (not scoped to sessions).
/// </summary>
public class RagRetrievalService(
    IEmbeddingService embeddingService,
    QdrantClient qdrantClient,
    ILogger<RagRetrievalService> logger) : IRagRetrievalService
{
    private const string CollectionName = "document_chunks";

    public async Task<List<RetrievedChunk>> SearchAsync(string query, int topK = 5, CancellationToken ct = default)
    {
        var collections = await qdrantClient.ListCollectionsAsync(ct);
        if (!collections.Any(c => c == CollectionName))
            return [];

        logger.LogInformation("RAG search: embedding query across all documents");

        var queryEmbedding = await embeddingService.GetEmbeddingAsync(query, ct);

        // Search all documents globally — no session filter
        var results = await qdrantClient.SearchAsync(
            CollectionName,
            queryEmbedding,
            limit: (ulong)topK,
            cancellationToken: ct);

        var chunks = results
            .Where(r => r.Score > 0.3f)
            .Select(r => new RetrievedChunk(
                r.Payload["text"].StringValue,
                r.Payload["file_name"].StringValue,
                (int)r.Payload["chunk_index"].IntegerValue,
                r.Score))
            .ToList();

        logger.LogInformation("RAG search returned {Count} relevant chunks (top score: {TopScore:F3})",
            chunks.Count, chunks.FirstOrDefault()?.Score ?? 0);

        return chunks;
    }
}
