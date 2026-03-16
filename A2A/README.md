# Agent-to-Agent (A2A) Protocol Implementation

This folder contains specialized AI agents that communicate via the [Google Agent-to-Agent (A2A) Protocol](https://github.com/google/a2a-protocol). Each agent is an independent service with standardized discovery, task management, and peer-to-peer messaging capabilities.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           A2A PROTOCOL LAYER                                │
│                                                                             │
│   Each agent exposes:                                                       │
│   • GET  /.well-known/agent.json  (Agent Card - discovery)                  │
│   • POST /a2a/message:send        (Send message, receive task)              │
│   • GET  /a2a/tasks/{taskId}      (Get task status and results)             │
│   • POST /a2a/tasks/{taskId}:cancel (Cancel a running task)                 │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
         ┌───────────────┬────────────┼────────────┬───────────────┐
         │               │            │            │               │
         ▼               ▼            ▼            ▼               │
    ┌─────────┐     ┌─────────┐  ┌─────────┐  ┌─────────┐          │
    │ Planner │◄───►│Reviewer │◄─┤Research │◄─┤  Code   │          │
    │  Agent  │     │  Agent  │  │  Agent  │  │  Agent  │          │
    └────┬────┘     └────┬────┘  └────┬────┘  └────┬────┘          │
         │               │            │            │               │
         └───────────────┴────────────┴────────────┘               │
                                      │                            │
                              ┌───────▼───────┐                    │
                              │    Ollama     │◄───────────────────┘
                              │   (llama3.1)  │
                              └───────────────┘
```

## Project Structure

```
A2A/
├── AspireOllama.A2A.Shared/         # Shared A2A protocol infrastructure
│   ├── AgentCard.cs                 # Agent Card model
│   ├── A2ATask.cs                   # Task lifecycle model
│   ├── A2AMessage.cs                # Message format
│   ├── A2AAgentClient.cs            # HTTP client for inter-agent calls
│   └── A2AServerBase.cs             # Base class for A2A servers
├── AspireOllama.A2A.PlannerAgent/   # Task planning and orchestration
│   ├── Program.cs                   # A2A endpoints
│   └── PlannerA2AServer.cs          # A2A implementation
├── AspireOllama.A2A.ReviewerAgent/  # Quality review and validation
│   ├── Program.cs
│   └── ReviewerA2AServer.cs
├── AspireOllama.A2A.ResearchAgent/  # Knowledge gathering
│   ├── Program.cs
│   └── ResearchA2AServer.cs
└── AspireOllama.A2A.CodeAgent/      # Code generation and execution
    ├── Program.cs
    └── CodeA2AServer.cs
```

## Agents

### 1. Planner Agent

**Purpose:** Breaks down complex tasks into actionable steps and orchestrates multi-agent workflows.

**Skills:**

| Skill | Description |
|-------|-------------|
| `create_plan` | Creates structured plan by breaking down complex tasks into steps |
| `assess_complexity` | Evaluates task complexity and determines required capabilities |
| `suggest_agents` | Recommends which specialist agents should handle parts of a task |
| `orchestrate_workflow` | Coordinates multi-agent tasks and determines next steps |

**Inter-Agent Calls:**
- Calls **Research Agent** to gather context before planning
- Calls **Reviewer Agent** to validate generated plans

### 2. Reviewer Agent

**Purpose:** Reviews and validates content, code, plans, and responses for quality assurance.

**Skills:**

| Skill | Description |
|-------|-------------|
| `review_response` | Reviews response for quality, accuracy, and completeness |
| `review_code` | Deep code review analyzing security, performance, and best practices |
| `review_plan` | Reviews task plans for completeness, feasibility, and proper agent assignments |
| `provide_feedback` | Provides detailed feedback to other agents about their work output |

### 3. Research Agent

**Purpose:** Gathers information, searches knowledge bases, and provides context.

**Skills:**

| Skill | Description |
|-------|-------------|
| `search_knowledge` | Searches knowledge base and synthesizes relevant information |
| `get_topic_details` | Retrieves comprehensive details about a specific topic |
| `gather_context` | Gathers context from multiple topics for complex tasks |
| `suggest_topics` | Suggests related research topics based on a query |

### 4. Code Agent

**Purpose:** Code generation, execution, analysis, and testing.

**Skills:**

| Skill | Description |
|-------|-------------|
| `execute_csharp` | Executes C# code in a sandboxed environment with timeout protection |
| `generate_code` | Generates code based on requirements using AI |
| `analyze_code` | Analyzes code structure, complexity, and patterns |
| `generate_tests` | Generates unit tests for code |
| `refactor_code` | Refactors code for improved readability, performance, or modularity |

**Inter-Agent Calls:**
- Calls **Reviewer Agent** for code review feedback

## A2A Protocol Details

### Agent Card

Each agent publishes an Agent Card at `/.well-known/agent.json`:

```json
{
  "name": "Planner Agent",
  "description": "AI-powered task planning and workflow orchestration agent",
  "version": "2.0.0",
  "url": "http://planner-agent",
  "provider": {
    "organization": "AspireOllama"
  },
  "capabilities": {
    "streaming": false,
    "pushNotifications": false
  },
  "skills": [
    {
      "id": "create_plan",
      "name": "Create Plan",
      "description": "Breaks complex tasks into executable steps with assigned agents",
      "tags": ["planning", "orchestration", "workflow"],
      "examples": ["Create a plan to build a REST API"]
    }
  ],
  "defaultInputModes": ["text/plain"],
  "defaultOutputModes": ["text/plain", "application/json"]
}
```

### Task Lifecycle

Tasks progress through these states:

| State | Description |
|-------|-------------|
| `Submitted` | Task received, not yet started |
| `Working` | Agent is processing (may include progress percentage) |
| `Completed` | Task finished successfully with artifacts |
| `Failed` | Task failed with error message |
| `Canceled` | Task was canceled by user |
| `InputRequired` | Agent needs more information |
| `AuthRequired` | Authentication needed |

### Send Message Request

```json
POST /a2a/message:send

{
  "message": {
    "messageId": "msg-123",
    "role": "user",
    "parts": [
      { "text": "Create a plan for building a REST API" }
    ]
  },
  "configuration": {
    "acceptedOutputModes": ["application/json"],
    "returnImmediately": false
  }
}
```

### Response

```json
{
  "task": {
    "id": "task-456",
    "contextId": "ctx-789",
    "status": {
      "state": "Completed",
      "message": "Plan created successfully",
      "progress": 100
    },
    "artifacts": [
      {
        "artifactId": "art-001",
        "parts": [
          {
            "data": { "title": "REST API Plan", "steps": [...] },
            "mediaType": "application/json"
          }
        ]
      }
    ],
    "history": [
      { "role": "user", "parts": [{ "text": "Create a plan..." }] },
      { "role": "agent", "parts": [{ "text": "Created plan with 5 steps" }] }
    ]
  }
}
```

## Service Connectivity

### Ollama Connection

Agents connect to Ollama using the `AddOlamaSharpClient` extension method which reads connection strings from Aspire:

```csharp
// Program.cs in each agent
builder.AddServiceDefaults();
builder.AddOlamaSharpClient("llama3.1");
```

This extension (defined in `ServiceDefaults/Extensions.cs`):
- Reads connection string from `ConnectionStrings:ollama`
- Parses `Endpoint=http://...` format from Aspire
- Falls back to `http://ollama` for local development
- Registers `Lazy<IOllamaApiClient>` for dependency injection

### Inter-Agent Communication

Agents discover each other via Aspire service discovery:

```csharp
// AppHost.cs
plannerAgent
    .WithEnvironment("A2A__KnownAgents__reviewer", reviewerAgent.GetEndpoint("http"))
    .WithEnvironment("A2A__KnownAgents__research", researchAgent.GetEndpoint("http"))
    .WithEnvironment("A2A__KnownAgents__code", codeAgent.GetEndpoint("http"));

// Agents wait for Ollama to be ready
plannerAgent.WithReference(ollama).WaitFor(llama);
```

Inside an agent, call another agent:

```csharp
// PlannerA2AServer.cs
protected async Task<SendMessageResponse?> CallAgentAsync(string agentName, string message, CancellationToken ct)
{
    return await _a2aClient.SendMessageAsync(agentName, new SendMessageRequest
    {
        Message = new A2AMessage
        {
            Parts = [new A2APart { Text = message }]
        }
    }, ct);
}

// Usage in ProcessCreatePlan:
var researchResponse = await CallAgentAsync("research", $"Gather context for: {taskDescription}", ct);
```

## Running the Agents

### With Aspire (Recommended)

```bash
dotnet run --project AspireOllama.AppHost
```

All agents start automatically and are registered for service discovery.

### Standalone (Development)

```bash
cd A2A/AspireOllama.A2A.PlannerAgent
dotnet run
```

## Testing A2A Endpoints

### Get Agent Card

```bash
curl http://localhost:5001/.well-known/agent.json
```

### Send Message

```bash
curl -X POST http://localhost:5001/a2a/message:send \
  -H "Content-Type: application/json" \
  -d '{
    "message": {
      "parts": [{"text": "Create a plan for building a REST API"}]
    }
  }'
```

### Get Task Status

```bash
curl http://localhost:5001/a2a/tasks/{taskId}
```

### Cancel Task

```bash
curl -X POST http://localhost:5001/a2a/tasks/{taskId}:cancel
```

## Extending the System

### Adding a New Skill

In an existing agent's A2A server:

```csharp
// Add to GetAgentCard() skills list
new AgentSkill
{
    Id = "new_skill",
    Name = "New Skill",
    Description = "What this skill does",
    Tags = ["tag1", "tag2"],
    Examples = ["Example usage"]
}

// Add handler in ProcessMessageAsync
if (lowerText.Contains("new_skill_trigger"))
{
    await ProcessNewSkill(task, text, ct);
}
```

### Creating a New Agent

1. Create new project in `A2A/` folder
2. Reference `AspireOllama.A2A.Shared`
3. Create A2A server class inheriting `A2AServerBase`:

```csharp
public class MyA2AServer : A2AServerBase
{
    public override AgentCard GetAgentCard() => new()
    {
        Name = "My Agent",
        Description = "What this agent does",
        // ...
    };

    public override async Task<A2ATask> ProcessMessageAsync(A2AMessage message, CancellationToken ct)
    {
        var task = CreateTask(message);
        // Process message and update task
        return task;
    }
}
```

4. Configure endpoints in `Program.cs`
5. Register in `AppHost.cs`

## Security Considerations

- **Authentication:** Initial implementation uses Aspire service discovery (internal network only)
- **HTTPS:** Enable for production deployments
- **Rate Limiting:** Add to prevent agent flooding
- **Context Isolation:** Each task has isolated context

## References

- [A2A Protocol Specification](https://github.com/google/a2a-protocol)
- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [OllamaSharp](https://github.com/awaescher/OllamaSharp)
