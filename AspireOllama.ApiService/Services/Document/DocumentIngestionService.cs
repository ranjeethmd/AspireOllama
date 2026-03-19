using AspireOllama.ApiService.Services.Embedding;
using AspireOllama.Shared;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AspireOllama.ApiService.Services.Document;

public class DocumentIngestionService(
    IDocumentProcessingService docService,
    ITextChunkingService chunkingService,
    IEmbeddingService embeddingService,
    QdrantClient qdrantClient,
    ILogger<DocumentIngestionService> logger) : IDocumentIngestionService
{
    private const string CollectionName = "document_chunks";

    public async Task<int> IngestDocumentAsync(FileAttachment file, CancellationToken ct = default)
    {
        var text = docService.ExtractText(file);
        if (string.IsNullOrWhiteSpace(text))
        {
            logger.LogWarning("No text extracted from {FileName}, skipping ingestion", file.FileName);
            return 0;
        }

        logger.LogInformation("Extracted {Length} chars from {FileName}, chunking...", text.Length, file.FileName);

        var chunks = chunkingService.ChunkText(text, file.FileName);
        if (chunks.Count == 0)
            return 0;

        logger.LogInformation("Created {Count} chunks from {FileName}, generating embeddings...", chunks.Count, file.FileName);

        var embeddings = await embeddingService.GetEmbeddingsAsync(
            chunks.Select(c => c.Text).ToList(), ct);

        await EnsureCollectionAsync(embeddings[0].Length, ct);

        var points = chunks.Zip(embeddings, (chunk, embedding) => new PointStruct
        {
            Id = new PointId { Uuid = Guid.NewGuid().ToString() },
            Vectors = embedding,
            Payload =
            {
                ["file_name"] = chunk.FileName,
                ["chunk_index"] = chunk.Index,
                ["text"] = chunk.Text
            }
        }).ToList();

        await qdrantClient.UpsertAsync(CollectionName, points, cancellationToken: ct);

        logger.LogInformation("Ingested {Count} chunks from {FileName} into Qdrant", points.Count, file.FileName);
        return points.Count;
    }

    private async Task EnsureCollectionAsync(int vectorSize, CancellationToken ct)
    {
        var collections = await qdrantClient.ListCollectionsAsync(ct);
        if (collections.Any(c => c == CollectionName))
            return;

        await qdrantClient.CreateCollectionAsync(
            CollectionName,
            new VectorParams
            {
                Size = (ulong)vectorSize,
                Distance = Distance.Dot
            },
            cancellationToken: ct);

        logger.LogInformation("Created Qdrant collection '{Collection}' with vector size {Size} and Dot distance",
            CollectionName, vectorSize);
    }
}
