using AspireOllama.A2A.Protocol;
using Task = System.Threading.Tasks.Task;
using AspireOllama.A2A.PlannerAgent.Models.A2a;
using AspireOllama.A2A.Shared;
using AspireOllama.ServiceDefaults.Authentication;
using Microsoft.Extensions.Options;
using OllamaSharp;
using System.Text.Json;

namespace AspireOllama.A2A.PlannerAgent;

public class PlannerA2AServer : A2AServerBase
{
    private readonly Lazy<IOllamaApiClient> _ollama;
    private readonly KnownAgentsOptions _knownAgents;

    public PlannerA2AServer(
        Lazy<IOllamaApiClient> ollamaClient,
        IOptions<KnownAgentsOptions> knownAgents,
        IA2AAgentClient a2aClient,
        ILogger<PlannerA2AServer> logger) : base(logger, a2aClient)
    {
        _ollama = ollamaClient;
        _knownAgents = knownAgents.Value;
    }

    public override AgentCard GetAgentCard() => new()
    {
        Name = "Planner Agent",
        Description = "AI-powered task planning and workflow orchestration agent. Breaks down complex tasks, assesses complexity, suggests appropriate agents, and orchestrates multi-agent workflows.",
        Version = "2.0.0",
        Url = "http://planner-agent",
        Provider = new AgentProvider { Organization = "AspireOllama" },
        Capabilities = new AgentCapabilities { Streaming = false, PushNotifications = false },
        Skills =
        [
            new AgentSkill
            {
                Id = "create_plan",
                Name = "Create Plan",
                Description = "Breaks complex tasks into executable steps with assigned agents",
                Tags = ["planning", "orchestration", "workflow"],
                Examples = ["Create a plan to build a REST API", "Plan the implementation of user authentication"]
            },
            new AgentSkill
            {
                Id = "assess_complexity",
                Name = "Assess Complexity",
                Description = "Evaluates task scope and determines required capabilities",
                Tags = ["analysis", "assessment"],
                Examples = ["Assess the complexity of migrating a database", "How complex is implementing SSO?"]
            },
            new AgentSkill
            {
                Id = "suggest_agents",
                Name = "Suggest Agents",
                Description = "Recommends which agents should handle components of a task",
                Tags = ["routing", "delegation"],
                Examples = ["Which agents should handle code review?", "Suggest agents for documentation task"]
            }
        ]
    };

    public override string? ResolveSkill(Message message)
    {
        var text = GetTextFromMessage(message).ToLowerInvariant();

        return text switch
        {
            _ when text.Contains("assess") || text.Contains("complexity") => "assess_complexity",
            _ when text.Contains("suggest") && text.Contains("agent") => "suggest_agents",
            _ => "create_plan"
        };
    }

    public override IReadOnlyDictionary<string, string> GetSkillRoles()
        => AuthRoles.A2ASkillRoles.GetValueOrDefault("planner") ?? new Dictionary<string, string>();

    public override async Task<Protocol.Task> ProcessMessageAsync(Message message, CancellationToken ct)
    {
        var task = CreateTask(message);
        UpdateTaskStatus(task, TaskState.Working, "Processing request...");

        var text = GetTextFromMessage(message);
        _logger.LogInformation("Processing planner request: {Text}", text.Length > 100 ? text[..100] + "..." : text);

        try
        {
            // Determine intent and process accordingly
            var lowerText = text.ToLowerInvariant();

            if (lowerText.Contains("assess") || lowerText.Contains("complexity"))
            {
                await ProcessAssessComplexity(task, text, ct);
            }
            else if (lowerText.Contains("suggest") && lowerText.Contains("agent"))
            {
                await ProcessSuggestAgents(task, text, ct);
            }
            else
            {
                // Default: Create a plan
                await ProcessCreatePlan(task, text, ct);
            }

            UpdateTaskStatus(task, TaskState.Completed, "Request processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing planner request");
            UpdateTaskStatus(task, TaskState.Failed, $"Error: {ex.Message}");
        }

        return task;
    }

    private async Task ProcessCreatePlan(Protocol.Task task, string taskDescription, CancellationToken ct)
    {
        UpdateTaskStatus(task, TaskState.Working, "Creating plan...", 10);

        // First, gather context from research agent if available
        string? researchContext = null;
        if (_a2aClient is not null && _knownAgents.Agents.ContainsKey("research"))
        {
            UpdateTaskStatus(task, TaskState.Working, "Gathering context from Research Agent...", 20);
            var researchResponse = await CallAgentAsync("research", $"Gather context for: {taskDescription}", ct);
            if (researchResponse?.Task?.Status.State == TaskState.Completed)
            {
                researchContext = GetTextFromArtifact(researchResponse.Task.Artifacts.FirstOrDefault() ?? new Artifact());
            }
        }

        UpdateTaskStatus(task, TaskState.Working, "Generating plan with AI...", 50);

        var prompt = "You are an expert project planner. Create a detailed plan for:\n\n" + taskDescription + "\n\n" +
            (researchContext is not null ? "Context from research:\n" + researchContext + "\n\n" : "") +
            "Respond in JSON: {\"title\": \"Plan Title\", \"steps\": [{\"step\": 1, \"action\": \"action\", \"agent\": \"planner|research|code|reviewer\", \"details\": \"details\"}], \"estimatedComplexity\": \"low|medium|high\", \"summary\": \"brief summary\"}";

        var result = await _ollama.Value.GenerateAsync(prompt, cancellationToken: ct).StreamToEndAsync();
        var content = result?.Response ?? "";

        var plan = ParseJsonResponse<PlanResult>(content);
        if (plan is not null)
        {
            AddArtifact(task, plan, "Generated Plan");
            AddResponseToHistory(task, $"Created plan: {plan.Title} with {plan.Steps?.Count ?? 0} steps");
        }
        else
        {
            AddTextArtifact(task, content, "Plan Response");
        }
    }

    private async Task ProcessAssessComplexity(Protocol.Task task, string taskDescription, CancellationToken ct)
    {
        UpdateTaskStatus(task, TaskState.Working, "Assessing complexity...", 30);

        var prompt = "Assess the complexity of this task:\n\n" + taskDescription + "\n\n" +
            "Respond in JSON: {\"complexityLevel\": \"low|medium|high|critical\", \"score\": 1-10, \"factors\": [\"factor1\"], \"needsPlanning\": true, \"needsResearch\": false, \"needsCode\": true, \"needsReview\": true, \"reasoning\": \"explanation\"}";

        var result = await _ollama.Value.GenerateAsync(prompt, cancellationToken: ct).StreamToEndAsync();
        var content = result?.Response ?? "";

        var assessment = ParseJsonResponse<ComplexityAssessment>(content);
        if (assessment is not null)
        {
            AddArtifact(task, assessment, "Complexity Assessment");
            AddResponseToHistory(task, $"Complexity: {assessment.ComplexityLevel} (Score: {assessment.Score}/10)");
        }
        else
        {
            AddTextArtifact(task, content, "Assessment Response");
        }
    }

    private async Task ProcessSuggestAgents(Protocol.Task task, string taskDescription, CancellationToken ct)
    {
        UpdateTaskStatus(task, TaskState.Working, "Suggesting agents...", 30);

        var availableAgents = string.Join(", ", _knownAgents.Agents.Keys.Prepend("planner"));

        var prompt = "Suggest which agents should handle this task:\n\n" + taskDescription + "\n\n" +
            "Available agents: " + availableAgents + "\n\n" +
            "Agent capabilities:\n" +
            "- planner: Task planning, workflow orchestration\n" +
            "- research: Knowledge gathering, context building\n" +
            "- code: Code generation, execution, analysis, testing\n" +
            "- reviewer: Quality review, feedback, validation\n\n" +
            "Respond in JSON: {\"suggestions\": [{\"agent\": \"name\", \"role\": \"role\", \"priority\": 1, \"reason\": \"why\"}], \"workflow\": \"recommended order\"}";

        var result = await _ollama.Value.GenerateAsync(prompt, cancellationToken: ct).StreamToEndAsync();
        var content = result?.Response ?? "";

        var suggestions = ParseJsonResponse<AgentSuggestions>(content);
        if (suggestions is not null)
        {
            AddArtifact(task, suggestions, "Agent Suggestions");
            var agentList = string.Join(", ", suggestions.Suggestions?.Select(s => s.Agent) ?? []);
            AddResponseToHistory(task, $"Suggested agents: {agentList}");
        }
        else
        {
            AddTextArtifact(task, content, "Suggestions Response");
        }
    }

}
