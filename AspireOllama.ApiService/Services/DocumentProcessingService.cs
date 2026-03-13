using System.Text;
using AspireOllama.Shared;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace AspireOllama.ApiService.Services;

public class DocumentProcessingService
{
    private readonly ILogger<DocumentProcessingService> _logger;

    public DocumentProcessingService(ILogger<DocumentProcessingService> logger)
    {
        _logger = logger;
    }

    public string ExtractText(FileAttachment file)
    {
        try
        {
            var bytes = Convert.FromBase64String(file.Base64Data);

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

    private string ExtractFromWord(byte[] bytes)
    {
        var sb = new StringBuilder();

        using var stream = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(stream, false);

        var body = doc.MainDocumentPart?.Document?.Body;
        if (body != null)
        {
            foreach (var paragraph in body.Elements<Paragraph>())
            {
                sb.AppendLine(paragraph.InnerText);
            }
        }

        return sb.ToString().Trim();
    }

    private string ExtractFromExcel(byte[] bytes)
    {
        var sb = new StringBuilder();

        using var stream = new MemoryStream(bytes);
        using var doc = SpreadsheetDocument.Open(stream, false);

        var workbookPart = doc.WorkbookPart;
        if (workbookPart == null) return string.Empty;

        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

        foreach (var worksheetPart in workbookPart.WorksheetParts)
        {
            var sheet = worksheetPart.Worksheet;
            var sheetData = sheet.GetFirstChild<SheetData>();

            if (sheetData == null) continue;

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

    private string GetCellValue(Cell cell, SharedStringTable? sharedStrings)
    {
        if (cell.CellValue == null) return string.Empty;

        var value = cell.CellValue.Text;

        if (cell.DataType?.Value == CellValues.SharedString && sharedStrings != null)
        {
            if (int.TryParse(value, out var index))
            {
                var item = sharedStrings.ElementAt(index);
                return item.InnerText;
            }
        }

        return value;
    }

    private string ExtractFromPowerPoint(byte[] bytes)
    {
        var sb = new StringBuilder();

        using var stream = new MemoryStream(bytes);
        using var doc = PresentationDocument.Open(stream, false);

        var presentationPart = doc.PresentationPart;
        if (presentationPart == null) return string.Empty;

        var slideIds = presentationPart.Presentation.SlideIdList?.ChildElements;
        if (slideIds == null) return string.Empty;

        int slideNumber = 1;
        foreach (var slideId in slideIds.OfType<DocumentFormat.OpenXml.Presentation.SlideId>())
        {
            var slidePart = (SlidePart?)presentationPart.GetPartById(slideId.RelationshipId!);
            if (slidePart?.Slide == null) continue;

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

    private string ExtractFromText(byte[] bytes)
    {
        return Encoding.UTF8.GetString(bytes);
    }

    public static FileType DetermineFileType(string contentType, string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        // Check by content type first
        return contentType.ToLowerInvariant() switch
        {
            "application/pdf" => FileType.Pdf,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => FileType.Word,
            "application/msword" => FileType.Word,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => FileType.Excel,
            "application/vnd.ms-excel" => FileType.Excel,
            "application/vnd.openxmlformats-officedocument.presentationml.presentation" => FileType.PowerPoint,
            "application/vnd.ms-powerpoint" => FileType.PowerPoint,
            "text/plain" => FileType.Text,
            "text/csv" => FileType.Text,
            "text/markdown" => FileType.Text,
            "application/json" => FileType.Text,
            "application/xml" => FileType.Text,
            "text/xml" => FileType.Text,
            "image/jpeg" or "image/jpg" or "image/png" or "image/gif" or "image/webp" => FileType.Image,
            _ => DetermineByExtension(extension)
        };
    }

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
