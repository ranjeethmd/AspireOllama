using AspireOllama.A2A.PlannerAgent.Models.Mcp;
using ModelContextProtocol.Server;
using OllamaSharp;
using OllamaSharp.AsyncEnumerableExtensions;
using System.ComponentModel;
using System.Text.Json;

namespace AspireOllama.A2A.PlannerAgent.Tools;

[McpServerToolType]
public static class PlannerTools
{
    private static IOllamaApiClient? _ollamaClient;

    public static void Initialize(IOllamaApiClient client)
    {
        _ollamaClient = client;
    }

    [McpServerTool, Description("Creates a detailed plan by analyzing a task and breaking it into actionable steps using AI reasoning")]
    public static async Task<PlanResult> create_plan(
        [Description("The task or goal to plan for")] string task,
        [Description("Maximum number of steps (default: 5)")] int maxSteps = 5)
    {
        var client = GetClient();

        var prompt = "You are an expert project planner. Analyze this task and create a detailed execution plan.\n\n" +
            "Task: " + task + "\n\n" +
            "Create a plan with up to " + maxSteps + " steps. For each step provide:\n" +
            "1. A clear action name\n" +
            "2. A detailed description\n" +
            "3. Complexity estimate (low/medium/high)\n" +
            "4. Which specialist agent should handle it (CodeAgent, ResearchAgent, ReviewerAgent, or self)\n\n" +
            "Respond in JSON format:\n" +
            "{\n" +
            "  \"steps\": [\n" +
            "    {\n" +
            "      \"stepNumber\": 1,\n" +
            "      \"action\": \"action name\",\n" +
            "      \"description\": \"detailed description\",\n" +
            "      \"complexity\": \"low|medium|high\",\n" +
            "      \"assignedAgent\": \"agent name\"\n" +
            "    }\n" +
            "  ],\n" +
            "  \"reasoning\": \"brief explanation of the plan\"\n" +
            "}";

        try
        {
            var response = await client.GenerateAsync(prompt).StreamToEndAsync();
            var content = response?.Response ?? "";

            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<AIPlanResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (parsed?.Steps is not null)
                {
                    return new PlanResult
                    {
                        Task = task,
                        Steps = parsed.Steps.Take(maxSteps).Select((s, i) => new PlanStep
                        {
                            StepNumber = i + 1,
                            Action = s.Action ?? "",
                            Description = s.Description ?? "",
                            EstimatedComplexity = s.Complexity ?? "medium",
                            AssignedAgent = s.AssignedAgent ?? "self"
                        }).ToList(),
                        TotalSteps = Math.Min(parsed.Steps.Count, maxSteps),
                        Status = "planned",
                        AiReasoning = parsed.Reasoning ?? ""
                    };
                }
            }

            return CreateFallbackPlan(task, maxSteps, content);
        }
        catch (Exception ex)
        {
            return CreateFallbackPlan(task, maxSteps, "AI Error: " + ex.Message);
        }
    }

    [McpServerTool, Description("Assesses task complexity using AI analysis and recommends if multi-agent coordination is needed")]
    public static async Task<ComplexityAssessment> assess_complexity(
        [Description("The task to assess")] string task)
    {
        var client = GetClient();

        var prompt = "Analyze the complexity of this task and determine if it needs multi-agent coordination.\n\n" +
            "Task: " + task + "\n\n" +
            "Consider:\n" +
            "- Technical complexity\n" +
            "- Number of distinct skills needed\n" +
            "- Dependencies between subtasks\n" +
            "- Risk factors\n\n" +
            "Respond in JSON format:\n" +
            "{\n" +
            "  \"complexity\": \"low|medium|high\",\n" +
            "  \"needsPlanning\": true,\n" +
            "  \"needsResearch\": true,\n" +
            "  \"needsCodeGeneration\": false,\n" +
            "  \"needsReview\": true,\n" +
            "  \"reasoning\": \"explanation\",\n" +
            "  \"suggestedApproach\": \"brief recommendation\"\n" +
            "}";

        try
        {
            var response = await client.GenerateAsync(prompt).StreamToEndAsync();
            var content = response?.Response ?? "";

            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<AIComplexityResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (parsed is not null)
                {
                    return new ComplexityAssessment
                    {
                        Task = task,
                        Complexity = parsed.Complexity ?? "medium",
                        NeedsPlanning = parsed.NeedsPlanning,
                        NeedsResearch = parsed.NeedsResearch,
                        NeedsCodeGeneration = parsed.NeedsCodeGeneration,
                        NeedsReview = parsed.NeedsReview,
                        Reasoning = parsed.Reasoning ?? "",
                        SuggestedApproach = parsed.SuggestedApproach ?? ""
                    };
                }
            }

            return CreateFallbackAssessment(task, content);
        }
        catch (Exception ex)
        {
            return CreateFallbackAssessment(task, "AI Error: " + ex.Message);
        }
    }

    [McpServerTool, Description("Uses AI to determine which specialist agents should collaborate on a task")]
    public static async Task<AgentSuggestion> suggest_agents(
        [Description("The task to analyze")] string task)
    {
        var client = GetClient();

        var prompt = "You are an AI orchestrator. Determine which specialist agents should work on this task.\n\n" +
            "Task: " + task + "\n\n" +
            "Available Agents:\n" +
            "- PlannerAgent: Task decomposition, planning, coordination\n" +
            "- ResearchAgent: Information gathering, knowledge search, context building\n" +
            "- CodeAgent: Code generation, execution, analysis, testing\n" +
            "- ReviewerAgent: Quality review, validation, feedback\n\n" +
            "Respond in JSON format:\n" +
            "{\n" +
            "  \"agents\": [\n" +
            "    {\n" +
            "      \"name\": \"AgentName\",\n" +
            "      \"role\": \"what they will do\",\n" +
            "      \"reason\": \"why they are needed\",\n" +
            "      \"priority\": 1\n" +
            "    }\n" +
            "  ],\n" +
            "  \"workflow\": \"description of how agents should collaborate\",\n" +
            "  \"estimatedInteractions\": 3\n" +
            "}";

        try
        {
            var response = await client.GenerateAsync(prompt).StreamToEndAsync();
            var content = response?.Response ?? "";

            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<AIAgentSuggestionResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (parsed?.Agents is not null)
                {
                    return new AgentSuggestion
                    {
                        Task = task,
                        RecommendedAgents = parsed.Agents.Select(a => new AgentRecommendation
                        {
                            AgentName = a.Name ?? "",
                            Role = a.Role ?? "",
                            Reason = a.Reason ?? "",
                            Priority = a.Priority
                        }).ToList(),
                        ExecutionOrder = parsed.Agents.OrderBy(a => a.Priority).Select(a => a.Name ?? "").ToList(),
                        Workflow = parsed.Workflow ?? "",
                        EstimatedInteractions = parsed.EstimatedInteractions
                    };
                }
            }

            return CreateFallbackSuggestion(task);
        }
        catch
        {
            return CreateFallbackSuggestion(task);
        }
    }

    [McpServerTool, Description("Orchestrates a multi-agent workflow, coordinating between agents to complete a complex task")]
    public static async Task<OrchestrationResult> orchestrate_workflow(
        [Description("The task to orchestrate")] string task,
        [Description("JSON array of agent results from previous steps")] string previousResults = "[]")
    {
        var client = GetClient();

        var prompt = "You are the orchestrator of a multi-agent system. Based on the task and previous agent results, determine the next action.\n\n" +
            "Task: " + task + "\n\n" +
            "Previous Results: " + previousResults + "\n\n" +
            "Decide:\n" +
            "1. Is the task complete?\n" +
            "2. If not, which agent should act next?\n" +
            "3. What specific instruction should that agent receive?\n\n" +
            "Respond in JSON format:\n" +
            "{\n" +
            "  \"isComplete\": false,\n" +
            "  \"nextAgent\": \"AgentName\",\n" +
            "  \"nextAction\": \"specific instruction for the agent\",\n" +
            "  \"reasoning\": \"why this decision\",\n" +
            "  \"progress\": 50,\n" +
            "  \"summary\": \"current state summary\"\n" +
            "}";

        try
        {
            var response = await client.GenerateAsync(prompt).StreamToEndAsync();
            var content = response?.Response ?? "";

            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<AIOrchestrationResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (parsed is not null)
                {
                    return new OrchestrationResult
                    {
                        Task = task,
                        IsComplete = parsed.IsComplete,
                        NextAgent = parsed.NextAgent,
                        NextAction = parsed.NextAction ?? "",
                        Reasoning = parsed.Reasoning ?? "",
                        Progress = parsed.Progress,
                        Summary = parsed.Summary ?? ""
                    };
                }
            }

            return new OrchestrationResult
            {
                Task = task,
                IsComplete = false,
                NextAgent = "ResearchAgent",
                NextAction = "Gather information about the task",
                Reasoning = "Default fallback",
                Progress = 10,
                Summary = content
            };
        }
        catch (Exception ex)
        {
            return new OrchestrationResult
            {
                Task = task,
                IsComplete = false,
                Reasoning = "Error: " + ex.Message,
                Summary = "Orchestration failed"
            };
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

    private static PlanResult CreateFallbackPlan(string task, int maxSteps, string reason)
    {
        return new PlanResult
        {
            Task = task,
            Steps = new List<PlanStep>
            {
                new() { StepNumber = 1, Action = "Analyze", Description = "Understand the task requirements", EstimatedComplexity = "low", AssignedAgent = "self" },
                new() { StepNumber = 2, Action = "Research", Description = "Gather relevant information", EstimatedComplexity = "medium", AssignedAgent = "ResearchAgent" },
                new() { StepNumber = 3, Action = "Implement", Description = "Execute the main task", EstimatedComplexity = "high", AssignedAgent = "CodeAgent" },
                new() { StepNumber = 4, Action = "Review", Description = "Validate the results", EstimatedComplexity = "medium", AssignedAgent = "ReviewerAgent" }
            }.Take(maxSteps).ToList(),
            TotalSteps = Math.Min(4, maxSteps),
            Status = "planned",
            AiReasoning = reason
        };
    }

    private static ComplexityAssessment CreateFallbackAssessment(string task, string reason)
    {
        var words = task.Split(' ').Length;
        return new ComplexityAssessment
        {
            Task = task,
            Complexity = words > 20 ? "high" : words > 10 ? "medium" : "low",
            NeedsPlanning = words > 10,
            NeedsResearch = true,
            NeedsCodeGeneration = task.ToLower().Contains("code") || task.ToLower().Contains("implement"),
            NeedsReview = true,
            Reasoning = reason,
            SuggestedApproach = "Standard multi-agent workflow"
        };
    }

    private static AgentSuggestion CreateFallbackSuggestion(string task)
    {
        return new AgentSuggestion
        {
            Task = task,
            RecommendedAgents = new List<AgentRecommendation>
            {
                new() { AgentName = "PlannerAgent", Role = "Orchestration", Reason = "Coordinate the workflow", Priority = 1 },
                new() { AgentName = "ResearchAgent", Role = "Information", Reason = "Gather context", Priority = 2 },
                new() { AgentName = "CodeAgent", Role = "Implementation", Reason = "Execute technical tasks", Priority = 3 },
                new() { AgentName = "ReviewerAgent", Role = "Quality", Reason = "Validate results", Priority = 4 }
            },
            ExecutionOrder = new List<string> { "PlannerAgent", "ResearchAgent", "CodeAgent", "ReviewerAgent" },
            Workflow = "Sequential execution with feedback loops",
            EstimatedInteractions = 5
        };
    }
}
