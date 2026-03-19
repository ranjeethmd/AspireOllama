namespace AspireOllama.ApiService.Services.Document;

public interface ITextChunkingService
{
    List<TextChunk> ChunkText(string text, string fileName, int chunkSize = 512, int overlap = 64);
}

public record TextChunk(string Text, string FileName, int Index);
