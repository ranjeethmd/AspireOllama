namespace AspireOllama.Shared;

public class DocumentUploadRequest
{
    public List<FileAttachment> Files { get; set; } = [];
}

public class DocumentUploadResponse
{
    public int TotalChunks { get; set; }
    public List<DocumentUploadResult> Results { get; set; } = [];
}

public class DocumentUploadResult
{
    public string FileName { get; set; } = string.Empty;
    public int ChunksIngested { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}
