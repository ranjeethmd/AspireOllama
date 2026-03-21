namespace AspireOllama.ServiceDefaults.Authentication;

/// <summary>
/// Centralized RBAC role constants for all resource servers.
/// These map to Azure AD App Roles defined on the app registration.
/// Tokens carry these in the "roles" claim.
/// Usage: Roles(AuthRoles.ApiChatWrite) or [Authorize(Roles = "Api.Chat.Write")]
/// </summary>
public static class AuthRoles
{

    // ── API Service ──
    public const string ApiAccess = "Api.Access";
    public const string ApiAdmin = "Api.Admin";
    public const string ApiChatRead = "Api.Chat.Read";
    public const string ApiChatWrite = "Api.Chat.Write";
    public const string ApiSessionsManage = "Api.Sessions.Manage";
    public const string ApiDocumentsManage = "Api.Documents.Manage";

    // ── MCP Server ──
    public const string McpAccess = "Mcp.Access";
    public const string McpToolsGetTime = "Mcp.Tools.GetTime";
    public const string McpToolsGetWeather = "Mcp.Tools.GetWeather";
    public const string McpToolsConvertUnits = "Mcp.Tools.ConvertUnits";

    // ── Planner Agent ──
    public const string A2APlannerAccess = "A2A.Planner.Access";
    public const string A2APlannerCreatePlan = "A2A.Planner.CreatePlan";
    public const string A2APlannerAssessComplexity = "A2A.Planner.AssessComplexity";
    public const string A2APlannerSuggestAgents = "A2A.Planner.SuggestAgents";


    // ── Reviewer Agent ──
    public const string A2AReviewerAccess = "A2A.Reviewer.Access";
    public const string A2AReviewerReviewResponse = "A2A.Reviewer.ReviewResponse";
    public const string A2AReviewerReviewCode = "A2A.Reviewer.ReviewCode";
    public const string A2AReviewerReviewPlan = "A2A.Reviewer.ReviewPlan";
    public const string A2AReviewerProvideFeedback = "A2A.Reviewer.ProvideFeedback";

    // ── Research Agent ──
    public const string A2AResearchAccess = "A2A.Research.Access";
    public const string A2AResearchSearchKnowledge = "A2A.Research.SearchKnowledge";
    public const string A2AResearchGetTopicDetails = "A2A.Research.GetTopicDetails";
    public const string A2AResearchGatherContext = "A2A.Research.GatherContext";
    public const string A2AResearchSuggestTopics = "A2A.Research.SuggestTopics";

    // ── Coordinator Agent ──
    public const string A2ACoordinatorAccess = "A2A.Coordinator.Access";
    public const string A2ACoordinatorOrchestrate = "A2A.Coordinator.Orchestrate";

    // ── Code Agent ──
    public const string A2ACodeAccess = "A2A.Code.Access";
    public const string A2ACodeExecuteCsharp = "A2A.Code.ExecuteCsharp";
    public const string A2ACodeGenerateCode = "A2A.Code.GenerateCode";
    public const string A2ACodeAnalyzeCode = "A2A.Code.AnalyzeCode";
    public const string A2ACodeGenerateTests = "A2A.Code.GenerateTests";
    public const string A2ACodeRefactorCode = "A2A.Code.RefactorCode";

    /// <summary>
    /// MCP tool name → required role mapping.
    /// </summary>
    public static readonly Dictionary<string, string> McpToolRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["get_time"] = McpToolsGetTime,
        ["get_weather"] = McpToolsGetWeather,
        ["convert_units"] = McpToolsConvertUnits,
    };

    /// <summary>
    /// A2A skill ID → required role mapping for each agent.
    /// </summary>
    public static readonly Dictionary<string, Dictionary<string, string>> A2ASkillRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["coordinator"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["orchestrate_task"] = A2ACoordinatorOrchestrate,
        },
        ["planner"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["create_plan"] = A2APlannerCreatePlan,
            ["assess_complexity"] = A2APlannerAssessComplexity,
            ["suggest_agents"] = A2APlannerSuggestAgents,

        },
        ["reviewer"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["review_response"] = A2AReviewerReviewResponse,
            ["review_code"] = A2AReviewerReviewCode,
            ["review_plan"] = A2AReviewerReviewPlan,
            ["provide_feedback"] = A2AReviewerProvideFeedback,
        },
        ["research"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["search_knowledge"] = A2AResearchSearchKnowledge,
            ["get_topic_details"] = A2AResearchGetTopicDetails,
            ["gather_context"] = A2AResearchGatherContext,
            ["suggest_topics"] = A2AResearchSuggestTopics,
        },
        ["code"] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["execute_csharp"] = A2ACodeExecuteCsharp,
            ["generate_code"] = A2ACodeGenerateCode,
            ["analyze_code"] = A2ACodeAnalyzeCode,
            ["generate_tests"] = A2ACodeGenerateTests,
            ["refactor_code"] = A2ACodeRefactorCode,
        },
    };
}
