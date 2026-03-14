using AspireOllama.Shared;

namespace AspireOllama.ApiService.Services.Document;

/// <summary>
/// Interface for document text extraction operations.
/// Single responsibility: Extract text content from various document formats.
/// </summary>
public interface IDocumentProcessingService
{
    /// <summary>
    /// Extracts text from a file attachment based on its type.
    /// </summary>
    /// <param name="file">The file attachment containing base64-encoded data</param>
    /// <returns>Extracted text content or error message if extraction fails</returns>
    string ExtractText(FileAttachment file);

    /// <summary>
    /// Determines the file type from MIME content type or file extension.
    /// </summary>
    /// <param name="contentType">MIME content type (e.g., "application/pdf")</param>
    /// <param name="fileName">Original file name with extension</param>
    /// <returns>The determined FileType enum value</returns>
    FileType DetermineFileType(string contentType, string fileName);
}
