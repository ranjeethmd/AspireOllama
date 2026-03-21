using AspireOllama.ApiService.Services.Document;
using AspireOllama.Shared;
using FastEndpoints;
using static AspireOllama.ServiceDefaults.Authentication.AuthRoles;

namespace AspireOllama.ApiService.Endpoints;

/// <summary>
/// Uploads documents to the global RAG vector store.
/// Documents are extracted, chunked, embedded, and stored in Qdrant.
/// All chat sessions can then search this knowledge base.
/// </summary>
public class DocumentUploadEndpoint(
    IDocumentIngestionService ingestionService,
    ILogger<DocumentUploadEndpoint> logger) : Endpoint<DocumentUploadRequest, DocumentUploadResponse>
{
    public override void Configure()
    {
        Post("/documents/upload");
        Roles(ApiAdmin, ApiDocumentsManage);
        Summary(s =>
        {
            s.Summary = "Upload documents to RAG vector store";
            s.Description = "Extracts text, chunks, embeds, and stores documents in the global vector database. " +
                            "All chat sessions can search this knowledge base.";
        });
    }

    public override async Task HandleAsync(DocumentUploadRequest req, CancellationToken ct)
    {
        if (req.Files is not { Count: > 0 })
        {
            await Send.ResponseAsync(new DocumentUploadResponse
            {
                Results = [new DocumentUploadResult { Error = "At least one file is required", Success = false }]
            }, 400, ct);
            return;
        }

        logger.LogInformation("Document upload: {FileCount} files", req.Files.Count);

        var results = new List<DocumentUploadResult>();
        var totalChunks = 0;

        foreach (var file in req.Files)
        {
            try
            {
                logger.LogInformation("Ingesting {FileName} ({Type}, {Size} bytes)",
                    file.FileName, file.Type, file.Base64Data.Length);

                var chunks = await ingestionService.IngestDocumentAsync(file, ct);
                totalChunks += chunks;

                results.Add(new DocumentUploadResult
                {
                    FileName = file.FileName,
                    ChunksIngested = chunks,
                    Success = true
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to ingest {FileName}", file.FileName);
                results.Add(new DocumentUploadResult
                {
                    FileName = file.FileName,
                    ChunksIngested = 0,
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        logger.LogInformation("Document upload complete: {TotalChunks} chunks from {FileCount} files",
            totalChunks, req.Files.Count);

        await Send.OkAsync(new DocumentUploadResponse
        {
            TotalChunks = totalChunks,
            Results = results
        }, ct);
    }
}
