using System.ComponentModel;
using AspireOllama.ApiService.Services.Document;
using AspireOllama.Shared;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AspireOllama.ApiService.Services.Tools;

/// <summary>
/// Tool for analyzing documents (PDF, Word, Excel, PowerPoint, text files).
/// Extracts text and sends to Ollama for analysis.
/// </summary>
public class DocumentAnalysisTool : ITool
{
    private readonly IChatClient _chatClient;
    private readonly IDocumentProcessingService _docService;
    private readonly ILogger<DocumentAnalysisTool> _logger;
    private readonly bool _isEnabled;

    // Thread-local storage for pending documents
    private static readonly AsyncLocal<List<PendingDocument>> _pendingDocuments = new();

    public string Name => "document_analysis";
    public string Description => "Analyzes documents (PDF, Word, Excel, PowerPoint, text) to extract and understand their content";
    public bool IsEnabled => _isEnabled;

    public DocumentAnalysisTool(
        [FromKeyedServices("tools")] IChatClient chatClient,
        IDocumentProcessingService docService,
        IOptions<ToolConfiguration> config,
        ILogger<DocumentAnalysisTool> logger)
    {
        _chatClient = chatClient;
        _docService = docService;
        _logger = logger;
        _isEnabled = config.Value.EnableDocumentAnalysis;
    }

    /// <summary>
    /// Sets documents for the current request context.
    /// </summary>
    public static void SetPendingDocuments(List<PendingDocument> documents)
    {
        _pendingDocuments.Value = documents;
    }

    /// <summary>
    /// Clears pending documents after request completion.
    /// </summary>
    public static void ClearPendingDocuments()
    {
        _pendingDocuments.Value = new List<PendingDocument>();
    }

    /// <summary>
    /// Gets the pending documents for the current request.
    /// </summary>
    public static List<PendingDocument>? GetPendingDocuments()
    {
        return _pendingDocuments.Value;
    }

    /// <summary>
    /// Analyzes uploaded documents with a specific question or instruction.
    /// </summary>
    [Description("Analyzes documents (PDF, Word, Excel, PowerPoint, text files) to understand their content, extract information, summarize, or answer questions about them. Use this when the user uploads a document and wants analysis.")]
    public async Task<string> AnalyzeDocumentAsync(
        [Description("The question or instruction about the document(s). Examples: 'Summarize this document', 'What are the key points?', 'Extract all dates mentioned', 'What is the total amount?'")]
        string instruction,
        CancellationToken cancellationToken = default)
    {
        var pendingDocs = _pendingDocuments.Value;

        if (pendingDocs == null || pendingDocs.Count == 0)
        {
            _logger.LogWarning("AnalyzeDocument called but no documents available");
            return "No documents are available for analysis. Please ask the user to upload a document first.";
        }

        _logger.LogInformation("Analyzing {Count} document(s) with instruction: {Instruction}",
            pendingDocs.Count, instruction);

        try
        {
            // Extract text from all documents
            var documentContents = new List<string>();
            foreach (var doc in pendingDocs)
            {
                var extractedText = _docService.ExtractText(doc.Attachment);
                if (!string.IsNullOrWhiteSpace(extractedText))
                {
                    documentContents.Add($"=== {doc.FileName} ===\n{extractedText}\n=== End of {doc.FileName} ===");
                    _logger.LogInformation("Extracted {Length} chars from {FileName}", extractedText.Length, doc.FileName);
                }
                else
                {
                    _logger.LogWarning("No text extracted from {FileName}", doc.FileName);
                    documentContents.Add($"=== {doc.FileName} ===\n[Could not extract text from this file]\n=== End of {doc.FileName} ===");
                }
            }

            var combinedContent = string.Join("\n\n", documentContents);

            // Build prompt for analysis
            var analysisPrompt = $"""
                Analyze the following document(s) and respond to this request: {instruction}

                DOCUMENT CONTENT:
                {combinedContent}

                Provide a clear, helpful response based on the document content above.
                """;

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, analysisPrompt)
            };

            // Call Ollama for analysis
            var response = await _chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            var result = response.Text ?? "Unable to analyze the document.";

            _logger.LogInformation("Document analysis completed, response length: {Length}", result.Length);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze documents");
            return $"Failed to analyze document: {ex.Message}";
        }
    }
}

/// <summary>
/// Represents a pending document for analysis.
/// </summary>
public class PendingDocument
{
    public required string FileName { get; set; }
    public required FileAttachment Attachment { get; set; }
}
