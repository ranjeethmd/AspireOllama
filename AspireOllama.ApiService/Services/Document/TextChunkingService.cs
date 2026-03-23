namespace AspireOllama.ApiService.Services.Document;

public class TextChunkingService : ITextChunkingService
{
    public List<TextChunk> ChunkText(string text, string fileName, int chunkSize = 512, int overlap = 64)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        if (overlap >= chunkSize)
            throw new ArgumentException($"Overlap ({overlap}) must be less than chunk size ({chunkSize}).");

        var chunks = new List<TextChunk>();
        var index = 0;
        var position = 0;

        while (position < text.Length)
        {
            var end = Math.Min(position + chunkSize, text.Length);
            var chunk = text[position..end];

            // Try to break at a sentence or paragraph boundary
            if (end < text.Length)
            {
                var lastBreak = chunk.LastIndexOfAny(['.', '\n', '!', '?']);
                if (lastBreak > chunkSize / 2)
                {
                    chunk = chunk[..(lastBreak + 1)];
                    end = position + lastBreak + 1;
                }
            }

            chunks.Add(new TextChunk(chunk.Trim(), fileName, index));
            index++;

            // Move forward by chunk length minus overlap
            position = Math.Max(position + 1, end - overlap);
        }

        return chunks;
    }
}
