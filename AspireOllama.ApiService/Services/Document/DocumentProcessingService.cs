using System.Text;
using AspireOllama.Shared;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace AspireOllama.ApiService.Services.Document;

/// <summary>
/// Extracts text content from various document formats (PDF, Word, Excel, PowerPoint, Text).
/// Single responsibility: Convert uploaded documents into plain text for AI analysis.
/// </summary>
public class DocumentProcessingService : IDocumentProcessingService
{
    private readonly ILogger<DocumentProcessingService> _logger;

    public DocumentProcessingService(ILogger<DocumentProcessingService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public string ExtractText(FileAttachment file)
    {
        try
        {
            // Decode base64 data to bytes
            var bytes = Convert.FromBase64String(file.Base64Data);

            // Route to appropriate extractor based on file type
            return file.Type switch
            {
                FileType.Pdf => ExtractFromPdf(bytes),
                FileType.Word => ExtractFromWord(bytes),
                FileType.Excel => ExtractFromExcel(bytes),
                FileType.PowerPoint => ExtractFromPowerPoint(bytes),
                FileType.Text => ExtractFromText(bytes),
                _ => $"[Unsupported file: {file.FileName}]"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from {FileName}", file.FileName);
            return $"[Error reading file: {file.FileName}]";
        }
    }

    /// <inheritdoc />
    public FileType DetermineFileType(string contentType, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        // Try to determine type from MIME content type first (more reliable)
        return contentType.ToLowerInvariant() switch
        {
            // PDF
            "application/pdf" => FileType.Pdf,

            // Microsoft Word (modern .docx and legacy .doc)
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => FileType.Word,
            "application/msword" => FileType.Word,

            // Microsoft Excel (modern .xlsx and legacy .xls)
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => FileType.Excel,
            "application/vnd.ms-excel" => FileType.Excel,

            // Microsoft PowerPoint (modern .pptx and legacy .ppt)
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => FileType.PowerPoint,
            "application/vnd.ms-powerpoint" => FileType.PowerPoint,

            // Text-based files
            "text/plain" => FileType.Text,
            "text/csv" => FileType.Text,
            "text/markdown" => FileType.Text,
            "application/json" => FileType.Text,
            "application/xml" => FileType.Text,
            "text/xml" => FileType.Text,

            // Images (for vision model)
            "image/jpeg" or "image/jpg" or "image/png" or "image/gif" or "image/webp" => FileType.Image,

            // Fall back to extension-based detection
            _ => DetermineByExtension(extension)
        };
    }

    // ============================================================
    // Private Extraction Methods
    // ============================================================

    /// <summary>
    /// Extracts text from PDF files using PdfPig library.
    /// </summary>
    private string ExtractFromPdf(byte[] bytes)
    {
        var sb = new StringBuilder();

        using var stream = new MemoryStream(bytes);
        using var document = PdfDocument.Open(stream);

        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Extracts text from Word documents (.docx) using OpenXml.
    /// </summary>
    private string ExtractFromWord(byte[] bytes)
    {
        var sb = new StringBuilder();

        using var stream = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(stream, false);

        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is not null)
        {
            foreach (var paragraph in body.Elements<Paragraph>())
            {
                sb.AppendLine(paragraph.InnerText);
            }
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Extracts text from Excel spreadsheets (.xlsx) using OpenXml.
    /// </summary>
    private string ExtractFromExcel(byte[] bytes)
    {
        var sb = new StringBuilder();

        using var stream = new MemoryStream(bytes);
        using var doc = SpreadsheetDocument.Open(stream, false);

        var workbookPart = doc.WorkbookPart;
        if (workbookPart is null) return string.Empty;

        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

        foreach (var worksheetPart in workbookPart.WorksheetParts)
        {
            var sheet = worksheetPart.Worksheet;
            var sheetData = sheet?.GetFirstChild<SheetData>();

            if (sheetData is null) continue;

            foreach (var row in sheetData.Elements<Row>())
            {
                var rowValues = new List<string>();

                foreach (var cell in row.Elements<Cell>())
                {
                    var value = GetCellValue(cell, sharedStrings);
                    rowValues.Add(value);
                }

                sb.AppendLine(string.Join("\t", rowValues));
            }

            sb.AppendLine();
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Resolves the actual cell value from Excel.
    /// </summary>
    private static string GetCellValue(Cell cell, SharedStringTable? sharedStrings)
    {
        if (cell.CellValue is null) return string.Empty;

        var value = cell.CellValue.Text;

        if (cell.DataType?.Value == CellValues.SharedString && sharedStrings is not null)
        {
            if (int.TryParse(value, out var index))
            {
                var item = sharedStrings.ElementAt(index);
                return item.InnerText;
            }
        }

        return value;
    }

    /// <summary>
    /// Extracts text from PowerPoint presentations (.pptx) using OpenXml.
    /// </summary>
    private string ExtractFromPowerPoint(byte[] bytes)
    {
        var sb = new StringBuilder();

        using var stream = new MemoryStream(bytes);
        using var doc = PresentationDocument.Open(stream, false);

        var presentationPart = doc.PresentationPart;
        if (presentationPart is null) return string.Empty;

        var slideIds = presentationPart.Presentation?.SlideIdList?.ChildElements;
        if (slideIds is null) return string.Empty;

        int slideNumber = 1;
        foreach (var slideId in slideIds.OfType<DocumentFormat.OpenXml.Presentation.SlideId>())
        {
            var slidePart = (SlidePart?)presentationPart.GetPartById(slideId.RelationshipId!);
            if (slidePart?.Slide is null) continue;

            sb.AppendLine($"--- Slide {slideNumber} ---");

            var texts = slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>();
            foreach (var text in texts)
            {
                if (!string.IsNullOrWhiteSpace(text.Text))
                {
                    sb.AppendLine(text.Text);
                }
            }

            sb.AppendLine();
            slideNumber++;
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Extracts text from plain text files.
    /// </summary>
    private static string ExtractFromText(byte[] bytes)
    {
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Determines file type based on file extension.
    /// </summary>
    private static FileType DetermineByExtension(string extension)
    {
        return extension switch
        {
            ".pdf" => FileType.Pdf,
            ".docx" => FileType.Word,
            ".doc" => FileType.Word,
            ".xlsx" => FileType.Excel,
            ".xls" => FileType.Excel,
            ".pptx" => FileType.PowerPoint,
            ".ppt" => FileType.PowerPoint,
            ".txt" => FileType.Text,
            ".csv" => FileType.Text,
            ".md" => FileType.Text,
            ".json" => FileType.Text,
            ".xml" => FileType.Text,
            ".log" => FileType.Text,
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => FileType.Image,
            _ => FileType.Unknown
        };
    }
}
