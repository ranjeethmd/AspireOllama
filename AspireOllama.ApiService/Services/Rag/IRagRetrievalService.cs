namespace AspireOllama.ApiService.Services.Rag;

public interface IRagRetrievalService
{
    Task<List<RetrievedChunk>> SearchAsync(string query, int topK = 5, CancellationToken ct = default);
}

public record RetrievedChunk(string Text, string FileName, int ChunkIndex, double Score);
