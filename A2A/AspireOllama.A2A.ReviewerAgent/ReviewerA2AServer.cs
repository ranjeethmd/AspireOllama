using AspireOllama.A2A.ReviewerAgent.Models.A2a;
using AspireOllama.A2A.Shared;
using AspireOllama.ServiceDefaults.Authentication;
using Microsoft.Extensions.Options;
using OllamaSharp;
using System.Text.Json;

namespace AspireOllama.A2A.ReviewerAgent;

public class ReviewerA2AServer : A2AServerBase
{
    private readonly Lazy<IOllamaApiClient> _ollama;
    private readonly KnownAgentsOptions _knownAgents;

    public ReviewerA2AServer(
        Lazy<IOllamaApiClient> ollamaClient,
        IOptions<KnownAgentsOptions> knownAgents,
        IA2AAgentClient a2aClient,
        ILogger<ReviewerA2AServer> logger) : base(logger, a2aClient)
    {
        _ollama = ollamaClient;
        _knownAgents = knownAgents.Value;
    }

    public override AgentCard GetAgentCard() => new()
    {
        Name = "Reviewer Agent",
        Description = "AI-powered quality assurance agent. Reviews responses, code, and work output for quality, accuracy, and completeness. Provides detailed feedback and improvement suggestions.",
        Version = "2.0.0",
        Url = "http://reviewer-agent",
        Provider = new AgentProvider { Organization = "AspireOllama" },
        Capabilities = new AgentCapabilities { Streaming = false, PushNotifications = false },
        Skills =
        [
            new AgentSkill
            {
                Id = "review_response",
                Name = "Review Response",
                Description = "Evaluates response quality on relevance, completeness, accuracy, and clarity",
                Tags = ["review", "quality", "validation"],
                Examples = ["Review this answer for accuracy", "Evaluate the quality of this response"]
            },
            new AgentSkill
            {
                Id = "review_code",
                Name = "Review Code",
                Description = "Deep code review analyzing security, performance, patterns, and best practices",
                Tags = ["code-review", "security", "quality"],
                Examples = ["Review this code for security issues", "Analyze code quality and suggest improvements"]
            },
            new AgentSkill
            {
                Id = "review_plan",
                Name = "Review Plan",
                Description = "Reviews task plans for completeness, feasibility, and proper agent assignments",
                Tags = ["review", "planning", "validation"],
                Examples = ["Review this implementation plan", "Validate the task breakdown"]
            },
            new AgentSkill
            {
                Id = "provide_feedback",
                Name = "Provide Feedback",
                Description = "Provides detailed feedback to other agents about their work output",
                Tags = ["feedback", "improvement", "collaboration"],
                Examples = ["Give feedback on the planner's output", "Review and critique this work"]
            }
        ]
    };

    public override string? ResolveSkill(A2AMessage message)
    {
        var text = GetTextFromMessage(message).ToLowerInvariant();
        return text switch
        {
            _ when text.Contains("code") => "review_code",
            _ when text.Contains("plan") => "review_plan",
            _ when text.Contains("feedback") || text.Contains("agent") => "provide_feedback",
            _ => "review_response"
        };
    }

    public override IReadOnlyDictionary<string, string> GetSkillRoles()
        => AuthRoles.A2ASkillRoles.GetValueOrDefault("reviewer") ?? new Dictionary<string, string>();

    public override async Task<A2ATask> ProcessMessageAsync(A2AMessage message, CancellationToken ct)
    {
        var task = CreateTask(message);
        UpdateTaskStatus(task, TaskState.Working, "Processing review request...");

        var text = GetTextFromMessage(message);
        _logger.LogInformation("Processing reviewer request: {Text}", text.Length > 100 ? text[..100] + "..." : text);

        try
        {
            var lowerText = text.ToLowerInvariant();

            if (lowerText.Contains("code"))
            {
                await ProcessCodeReview(task, text, ct);
            }
            else if (lowerText.Contains("plan"))
            {
                await ProcessPlanReview(task, text, ct);
            }
            else if (lowerText.Contains("feedback") || lowerText.Contains("agent"))
            {
                await ProcessProvideFeedback(task, text, ct);
            }
            else
            {
                await ProcessReviewResponse(task, text, ct);
            }

            UpdateTaskStatus(task, TaskState.Completed, "Review completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing review request");
            UpdateTaskStatus(task, TaskState.Failed, $"Error: {ex.Message}");
        }

        return task;
    }

    private async Task ProcessReviewResponse(A2ATask task, string content, CancellationToken ct)
    {
        UpdateTaskStatus(task, TaskState.Working, "Reviewing response...", 30);

        var prompt = "You are an expert content reviewer. Analyze this content and evaluate its quality.\n\n" +
            "Content to Review:\n" + content + "\n\n" +
            "Evaluate on: Relevance, Completeness, Accuracy, Clarity, Helpfulness.\n\n" +
            "Respond in JSON: {\"approved\": true, \"score\": 85, \"issues\": [], \"summary\": \"assessment\", \"improvements\": []}";

        var stream = _ollama.Value.GenerateAsync(prompt: prompt, cancellationToken: ct);
        var result = await stream.StreamToEndAsync();
        var response = result?.Response ?? "";

        var review = ParseJsonResponse<ReviewResult>(response);
        if (review is not null)
        {
            AddArtifact(task, review, "Review Result");
            AddResponseToHistory(task, $"Review: {(review.Approved ? "Approved" : "Needs Improvement")} - Score: {review.Score}/100");
        }
        else
        {
            AddTextArtifact(task, response, "Review Response");
        }
    }

    private async Task ProcessPlanReview(A2ATask task, string content, CancellationToken ct)
    {
        UpdateTaskStatus(task, TaskState.Working, "Reviewing plan...", 30);

        var prompt = "You are an expert plan reviewer. Analyze this task plan:\n\n" +
            content + "\n\n" +
            "Evaluate: Completeness, Feasibility, Proper agent assignments, Step ordering, Risk assessment.\n\n" +
            "Respond in JSON: {\"approved\": true, \"score\": 85, \"issues\": [], \"summary\": \"assessment\", \"improvements\": [], \"missingSteps\": [], \"riskLevel\": \"low|medium|high\"}";

        var stream = _ollama.Value.GenerateAsync(prompt: prompt, cancellationToken: ct);
        var result = await stream.StreamToEndAsync();
        var response = result?.Response ?? "";

        var review = ParseJsonResponse<PlanReviewResult>(response);
        if (review is not null)
        {
            AddArtifact(task, review, "Plan Review Result");
            AddResponseToHistory(task, $"Plan Review: {(review.Approved ? "Approved" : "Needs Revision")} - Score: {review.Score}/100, Risk: {review.RiskLevel}");
        }
        else
        {
            AddTextArtifact(task, response, "Plan Review Response");
        }
    }

    private async Task ProcessCodeReview(A2ATask task, string content, CancellationToken ct)
    {
        UpdateTaskStatus(task, TaskState.Working, "Reviewing code...", 30);

        var prompt = "You are an expert code reviewer. Analyze this code:\n\n" +
            content + "\n\n" +
            "Review for: Security vulnerabilities, Performance issues, Code quality, Best practices, Potential bugs.\n\n" +
            "Respond in JSON: {\"approved\": true, \"issueCount\": 0, \"issues\": [{\"severity\": \"high|medium|low\", \"type\": \"type\", \"description\": \"desc\", \"suggestion\": \"fix\"}], \"summary\": \"assessment\", \"securityScore\": 80, \"qualityScore\": 80}";

        var stream = _ollama.Value.GenerateAsync(prompt: prompt, cancellationToken: ct);
        var result = await stream.StreamToEndAsync();
        var response = result?.Response ?? "";

        var review = ParseJsonResponse<CodeReviewResult>(response);
        if (review is not null)
        {
            AddArtifact(task, review, "Code Review Result");
            AddResponseToHistory(task, $"Code Review: {(review.Approved ? "Approved" : "Changes Required")} - {review.IssueCount} issues found");
        }
        else
        {
            AddTextArtifact(task, response, "Code Review Response");
        }
    }

    private async Task ProcessProvideFeedback(A2ATask task, string content, CancellationToken ct)
    {
        UpdateTaskStatus(task, TaskState.Working, "Generating feedback...", 30);

        var prompt = "You are a senior reviewer providing constructive feedback.\n\n" +
            "Work to Review:\n" + content + "\n\n" +
            "Provide detailed feedback. Respond in JSON: {\"verdict\": \"accept|revise|reject\", \"completionPercentage\": 80, \"strengths\": [], \"weaknesses\": [], \"detailedFeedback\": \"text\", \"shouldRetry\": false, \"nextSteps\": []}";

        var stream = _ollama.Value.GenerateAsync(prompt: prompt, cancellationToken: ct);
        var result = await stream.StreamToEndAsync();
        var response = result?.Response ?? "";


        var feedback = ParseJsonResponse<AgentFeedbackResult>(response);
        if (feedback is not null)
        {
            AddArtifact(task, feedback, "Agent Feedback");
            AddResponseToHistory(task, $"Feedback: {feedback.Verdict} - {feedback.CompletionPercentage}% complete");
        }
        else
        {
            AddTextArtifact(task, response, "Feedback Response");
        }
    }

    private static T? ParseJsonResponse<T>(string content) where T : class
    {
        try
        {
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
        }
        catch { }
        return null;
    }
}
