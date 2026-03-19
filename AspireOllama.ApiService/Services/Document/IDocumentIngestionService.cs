using AspireOllama.Shared;

namespace AspireOllama.ApiService.Services.Document;

public interface IDocumentIngestionService
{
    Task<int> IngestDocumentAsync(FileAttachment file, CancellationToken ct = default);
}
