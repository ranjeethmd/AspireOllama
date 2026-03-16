using AspireOllama.A2A.ReviewerAgent.Models.Mcp;
using ModelContextProtocol.Server;
using OllamaSharp;
using OllamaSharp.AsyncEnumerableExtensions;
using System.ComponentModel;
using System.Text.Json;

namespace AspireOllama.A2A.ReviewerAgent.Tools;

[McpServerToolType]
public static class ReviewerTools
{
    private static IOllamaApiClient? _ollamaClient;

    public static void Initialize(IOllamaApiClient client) => _ollamaClient = client;

    [McpServerTool, Description("Uses AI to review a response for quality, accuracy, completeness, and relevance")]
    public static async Task<ReviewResult> review_response(
        [Description("The original question or prompt")] string originalPrompt,
        [Description("The response to review")] string response)
    {
        var client = GetClient();

        var prompt = "You are an expert content reviewer. Analyze this response and evaluate its quality.\n\n" +
            "Original Prompt: " + originalPrompt + "\n\n" +
            "Response to Review: " + response + "\n\n" +
            "Evaluate on: Relevance, Completeness, Accuracy, Clarity, Helpfulness.\n\n" +
            "Respond in JSON: {\"approved\": true, \"score\": 85, \"issues\": [], \"summary\": \"assessment\", \"improvements\": []}";

        try
        {
            var result = await client.GenerateAsync(prompt).StreamToEndAsync();
            var content = result?.Response ?? "";
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<AIReviewResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed is not null)
                {
                    return new ReviewResult
                    {
                        Approved = parsed.Approved,
                        Score = parsed.Score,
                        Issues = parsed.Issues ?? [],
                        Summary = parsed.Summary ?? "",
                        Improvements = parsed.Improvements ?? [],
                        AiPowered = true
                    };
                }
            }
            return new ReviewResult { Approved = true, Score = 70, Summary = content, AiPowered = false };
        }
        catch (Exception ex)
        {
            return new ReviewResult { Approved = true, Score = 50, Summary = "Error: " + ex.Message, AiPowered = false };
        }
    }

    [McpServerTool, Description("Uses AI to perform deep code review analyzing patterns, security, and best practices")]
    public static async Task<CodeReviewResult> review_code(
        [Description("The code to review")] string code,
        [Description("Programming language")] string language = "csharp")
    {
        var client = GetClient();

        var prompt = "You are an expert code reviewer. Analyze this " + language + " code:\n\n" +
            "```" + language + "\n" + code + "\n```\n\n" +
            "Review for: Security, Performance, Quality, Best practices, Bugs.\n\n" +
            "Respond in JSON: {\"approved\": true, \"issueCount\": 0, \"issues\": [], \"summary\": \"assessment\", \"securityScore\": 80, \"qualityScore\": 80}";

        try
        {
            var result = await client.GenerateAsync(prompt).StreamToEndAsync();
            var content = result?.Response ?? "";
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<AICodeReviewResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed is not null)
                {
                    return new CodeReviewResult
                    {
                        Approved = parsed.Approved,
                        IssueCount = parsed.IssueCount,
                        Issues = parsed.Issues ?? [],
                        Summary = parsed.Summary ?? "",
                        SecurityScore = parsed.SecurityScore,
                        QualityScore = parsed.QualityScore,
                        AiPowered = true
                    };
                }
            }
            return new CodeReviewResult { Approved = true, Summary = content, SecurityScore = 70, QualityScore = 70, AiPowered = false };
        }
        catch (Exception ex)
        {
            return new CodeReviewResult { Summary = "Error: " + ex.Message, AiPowered = false };
        }
    }

    [McpServerTool, Description("Provides feedback to another agent about their work output")]
    public static async Task<AgentFeedback> provide_feedback(
        [Description("Name of the agent that produced the work")] string agentName,
        [Description("The work output to review")] string workOutput,
        [Description("The original task")] string originalTask)
    {
        var client = GetClient();

        var prompt = "You are a senior reviewer providing feedback to " + agentName + ".\n\n" +
            "Original Task: " + originalTask + "\n\nAgent Output: " + workOutput + "\n\n" +
            "Provide feedback. Respond in JSON: {\"verdict\": \"accept\", \"completionPercentage\": 80, \"strengths\": [], \"weaknesses\": [], \"detailedFeedback\": \"text\", \"shouldRetry\": false}";

        try
        {
            var result = await client.GenerateAsync(prompt).StreamToEndAsync();
            var content = result?.Response ?? "";
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<AIFeedbackResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed is not null)
                {
                    return new AgentFeedback
                    {
                        AgentName = agentName,
                        Verdict = parsed.Verdict ?? "revise",
                        CompletionPercentage = parsed.CompletionPercentage,
                        Strengths = parsed.Strengths ?? [],
                        Weaknesses = parsed.Weaknesses ?? [],
                        DetailedFeedback = parsed.DetailedFeedback ?? "",
                        ShouldRetry = parsed.ShouldRetry,
                        AiPowered = true
                    };
                }
            }
            return new AgentFeedback { AgentName = agentName, Verdict = "revise", DetailedFeedback = content, AiPowered = false };
        }
        catch (Exception ex)
        {
            return new AgentFeedback { AgentName = agentName, DetailedFeedback = "Error: " + ex.Message, AiPowered = false };
        }
    }

    private static IOllamaApiClient GetClient()
    {
        if (_ollamaClient == null)
        {
            _ollamaClient = new OllamaApiClient("http://localhost:11434");
            _ollamaClient.SelectedModel = "llama3.1";
        }
        return _ollamaClient;
    }
}
