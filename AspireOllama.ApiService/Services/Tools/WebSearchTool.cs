using AspireOllama.Shared;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Web;

namespace AspireOllama.ApiService.Services.Tools;

/// <summary>
/// Web search tool using SerpAPI (Google results) and optionally Bing.
/// SerpAPI handles Google search without requiring Google Cloud billing.
/// Config: Tools:WebSearch:SerpApiKey and/or Tools:WebSearch:BingApiKey
/// </summary>
public class WebSearchTool : ITool
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<WebSearchTool> _logger;
    private readonly bool _isEnabled;

    public string Name => "web_search";
    public string Description => "Searches Google (via SerpAPI) and Bing for current information from the internet";
    public bool IsEnabled => _isEnabled;

    public WebSearchTool(
        IHttpClientFactory httpClientFactory,
        IOptions<ToolConfiguration> toolConfig,
        IConfiguration config,
        ILogger<WebSearchTool> logger)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
        _isEnabled = toolConfig.Value.EnableWebSearch;
    }

    [Description("Search the internet for current information, news, or facts. Uses Google via SerpAPI and Bing.")]
    public async Task<string> SearchAsync(
        [Description("The chat session ID")] string sessionId,
        [Description("The search query")] string query,
        [Description("Maximum results (1-10, default 5)")] int maxResults = 5)
    {
        if (string.IsNullOrWhiteSpace(query))
            return "Error: Search query cannot be empty.";

        maxResults = Math.Clamp(maxResults, 1, 10);
        _logger.LogInformation("Web search: query='{Query}', max={Max}", query, maxResults);

        var client = _httpClientFactory.CreateClient();
        var tasks = new List<Task<List<WebSearchResult>>>();

        // SerpAPI (Google results)
        var serpApiKey = _config["Tools:WebSearch:SerpApiKey"];
        if (!string.IsNullOrEmpty(serpApiKey))
            tasks.Add(SearchSerpApiAsync(client, query, serpApiKey, maxResults));

        // Bing
        var bingKey = _config["Tools:WebSearch:BingApiKey"];
        if (!string.IsNullOrEmpty(bingKey))
            tasks.Add(SearchBingAsync(client, query, bingKey, maxResults));

        if (tasks.Count == 0)
            return "Web search not configured. Set Tools:WebSearch:SerpApiKey or Tools:WebSearch:BingApiKey.";

        await Task.WhenAll(tasks);

        var results = tasks.SelectMany(t => t.Result)
            .GroupBy(r => r.Url).Select(g => g.First())
            .Take(maxResults).ToList();

        if (results.Count == 0)
            return $"No results found for '{query}'.";

        _logger.LogInformation("Web search returned {Count} results for '{Query}'", results.Count, query);

        return $"Web search results for '{query}':\n\n" +
            string.Join("\n\n", results.Select((r, i) =>
                $"[{i + 1}] {r.Title}\n    URL: {r.Url}\n    {r.Snippet}"));
    }

    private async Task<List<WebSearchResult>> SearchSerpApiAsync(
        HttpClient client, string query, string apiKey, int max)
    {
        try
        {
            var url = $"https://serpapi.com/search.json?engine=google&q={HttpUtility.UrlEncode(query)}&api_key={apiKey}&num={max}";
            _logger.LogInformation("SerpAPI search for: {Query}", query);

            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync();
                _logger.LogWarning("SerpAPI failed: {Status} — {Body}", resp.StatusCode, body[..Math.Min(300, body.Length)]);
                return [];
            }

            var json = await resp.Content.ReadFromJsonAsync<SerpApiResponse>();
            var results = json?.OrganicResults?.Select(r => new WebSearchResult(
                r.Title ?? "", r.Link ?? "", r.Snippet ?? "", "Google")).ToList() ?? [];

            _logger.LogInformation("SerpAPI returned {Count} results", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SerpAPI search failed");
            return [];
        }
    }

    private async Task<List<WebSearchResult>> SearchBingAsync(
        HttpClient client, string query, string apiKey, int max)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.bing.microsoft.com/v7.0/search?q={HttpUtility.UrlEncode(query)}&count={max}");
            request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);

            var resp = await client.SendAsync(request);
            if (!resp.IsSuccessStatusCode) return [];

            var json = await resp.Content.ReadFromJsonAsync<BingResponse>();
            return json?.WebPages?.Value?.Select(i =>
                new WebSearchResult(i.Name ?? "", i.Url ?? "", i.Snippet ?? "", "Bing")).ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bing search failed");
            return [];
        }
    }
}

// ── Response Models ──

public record WebSearchResult(string Title, string Url, string Snippet, string Source);

// SerpAPI
public class SerpApiResponse
{
    [JsonPropertyName("organic_results")]
    public List<SerpApiResult>? OrganicResults { get; set; }
}
public class SerpApiResult
{
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("link")] public string? Link { get; set; }
    [JsonPropertyName("snippet")] public string? Snippet { get; set; }
}

// Bing
public class BingResponse
{
    [JsonPropertyName("webPages")] public BingPages? WebPages { get; set; }
}
public class BingPages
{
    [JsonPropertyName("value")] public List<BingPage>? Value { get; set; }
}
public class BingPage
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("snippet")] public string? Snippet { get; set; }
}
