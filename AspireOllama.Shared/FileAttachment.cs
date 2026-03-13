namespace AspireOllama.Shared;

public class FileAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Base64Data { get; set; } = string.Empty;
    public FileType Type { get; set; } = FileType.Unknown;
}

public enum FileType
{
    Unknown,
    Image,
    Pdf,
    Word,
    Excel,
    PowerPoint,
    Text
}
