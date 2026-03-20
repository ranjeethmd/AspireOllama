using System.ComponentModel;
using AspireOllama.ApiService.Services.Rag;

namespace AspireOllama.ApiService.Services.Tools;

public class RagSearchTool(IRagRetrievalService ragService, ILogger<RagSearchTool> logger) : ITool
{
    public string Name => "search_knowledge_base";
    public string Description => "Search uploaded documents for relevant information with relevance scores";
    public bool IsEnabled => true;

    [Description("Search the uploaded knowledge base for relevant information. Returns results ranked by relevance score (0-1). Only use results with high relevance (above 0.6) to answer questions. Ignore low-relevance results as noise.")]
    public async Task<string> SearchAsync(
        [Description("The chat session ID")] string session_id,
        [Description("The search query — be specific and use key terms from the user's question")] string query,
        [Description("Number of results to return (1-10, default 3)")] int top_k = 3,
        CancellationToken ct = default)
    {
        logger.LogInformation("RAG tool: session={SessionId}, query={Query}, topK={TopK}", session_id, query, top_k);
        var clampedK = Math.Clamp(top_k, 1, 10);
        var chunks = await ragService.SearchAsync(query, topK: clampedK, ct: ct);
        if (chunks.Count == 0)
            return "No relevant documents found in the knowledge base.";
        return $"Found {chunks.Count} results (ranked by relevance):\n\n" +
            string.Join("\n\n", chunks.Select(c =>
                $"[Relevance: {c.Score:F2}] [{c.FileName}, section {c.ChunkIndex}]:\n{c.Text}"));
    }
}
