namespace AspireOllama.Shared;

public class AgentInfo
{
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public List<AgentTool> Tools { get; set; } = [];
}

public class AgentTool
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
}

public class AgentCallRequest
{
    public string AgentName { get; set; } = "";
    public string ToolName { get; set; } = "";
    public Dictionary<string, object?> Arguments { get; set; } = [];
}

public class AgentCallResponse
{
    public string AgentName { get; set; } = "";
    public string ToolName { get; set; } = "";
    public bool Success { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }
    public long ExecutionTimeMs { get; set; }
}

public class AgentWorkflowRequest
{
    public string Task { get; set; } = "";
}

public class AgentWorkflowResponse
{
    public string Task { get; set; } = "";
    public List<AgentInteraction> Interactions { get; set; } = [];
    public string FinalResult { get; set; } = "";
    public long TotalExecutionTimeMs { get; set; }
}

public class AgentInteraction
{
    public int Step { get; set; }
    public string AgentName { get; set; } = "";
    public string ToolName { get; set; } = "";
    public Dictionary<string, object?> Arguments { get; set; } = [];
    public object? Result { get; set; }
    public long ExecutionTimeMs { get; set; }
    public string Status { get; set; } = "";
}
