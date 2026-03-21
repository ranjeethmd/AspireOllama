# Agent-to-Agent (A2A) Protocol Implementation

This folder contains specialized AI agents that communicate via the [Google Agent-to-Agent (A2A) Protocol](https://github.com/google/a2a-protocol). Each agent is an independent service with standardized discovery, task management, and peer-to-peer messaging capabilities.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           A2A PROTOCOL LAYER                                │
│                                                                             │
│   Each agent exposes:                                                       │
│   • GET  /.well-known/agent.json    (Agent Card - discovery, unauth)        │
│   • POST /a2a/message:send          (Send message, per-skill auth)          │
│   • POST /a2a/message:stream        (Stream response, per-skill auth)       │
│   • GET  /a2a/tasks/{taskId}        (Get task status and results)           │
│   • GET  /a2a/tasks                 (List all tasks)                        │
│   • POST /a2a/tasks/{taskId}:cancel (Cancel a running task)                 │
│   • POST /a2a/tasks/{taskId}:subscribe (Subscribe to task updates)          │
│   • POST /a2a/tasks/{taskId}/pushNotification (Push notification CRUD)      │
│   • GET  /a2a/agent/card            (Extended agent card, authenticated)    │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
    ┌─────────────────────────────────┼─────────────────────────────────┐
    │               │                 │                │                │
    ▼               ▼                 ▼                ▼                ▼
┌─────────┐  ┌───────────┐     ┌─────────┐     ┌─────────┐     ┌─────────┐
│Coordinator│  │ Planner  │◄───►│Reviewer │◄───►│Research │◄───►│  Code   │
│  Agent   │  │  Agent   │     │  Agent  │     │  Agent  │     │  Agent  │
└────┬─────┘  └────┬─────┘     └────┬────┘     └────┬────┘     └────┬────┘
     │              │               │               │               │
     │  1 skill     │  3 skills     │  4 skills     │  4 skills     │  5 skills
     │              │               │               │               │
     └──────────────┴───────────────┴───────────────┴───────────────┘
                                    │
                            ┌───────▼───────┐
                            │    Ollama     │
                            │  (Qwen3 32B) │
                            └───────────────┘
```

## Class Hierarchy

```
IA2AServer (interface — pure A2A protocol spec, 11 operations)
ISkillAuthorizationProvider (interface — per-skill role checks)
  └── A2AServerBase (abstract — implements both, virtual defaults)
       ├── CoordinatorA2AServer   (overrides ResolveSkill + GetSkillRoles)
       ├── PlannerA2AServer       (overrides ResolveSkill + GetSkillRoles)
       ├── ReviewerA2AServer      (overrides ResolveSkill + GetSkillRoles)
       ├── ResearchA2AServer      (overrides ResolveSkill + GetSkillRoles)
       └── CodeA2AServer          (overrides ResolveSkill + GetSkillRoles)

Extensions (A2A.Shared):
  AddA2AServices()       — known agents, HTTP clients, rate limiting
  AddA2AServer<T>()      — registers server singleton
  MapA2AEndpoints<T>()   — maps all endpoints, auth, rate limiting, skill auth, 501 for unsupported
```

## Project Structure

```
A2A/
├── AspireOllama.A2A.Shared/              # Shared A2A protocol infrastructure
│   ├── IA2AServer.cs                     # Pure A2A protocol interface (11 operations)
│   ├── ISkillAuthorizationProvider.cs    # Per-skill authorization interface
│   ├── A2AServerBase.cs                  # Abstract base (implements both interfaces)
│   ├── A2AHostExtensions.cs             # AddA2AServices, AddA2AServer, MapA2AEndpoints
│   ├── A2ARateLimiting.cs               # Per-user rate limiting (20 req/min)
│   ├── A2AAgentClient.cs               # HTTP client for inter-agent calls
│   ├── AgentCard.cs                     # Agent Card model
│   ├── A2ATask.cs                       # Task lifecycle model
│   ├── A2AMessage.cs                    # Message format
│   └── PushNotificationConfig.cs        # Push notification webhook config
├── AspireOllama.A2A.CoordinatorAgent/   # Workflow orchestrator (hub)
│   ├── Program.cs
│   └── CoordinatorA2AServer.cs
├── AspireOllama.A2A.PlannerAgent/       # Task planning
│   ├── Program.cs
│   └── PlannerA2AServer.cs
├── AspireOllama.A2A.ReviewerAgent/      # Quality review
│   ├── Program.cs
│   └── ReviewerA2AServer.cs
├── AspireOllama.A2A.ResearchAgent/      # Knowledge gathering
│   ├── Program.cs
│   └── ResearchA2AServer.cs
└── AspireOllama.A2A.CodeAgent/          # Code generation and execution
    ├── Program.cs
    └── CodeA2AServer.cs
```

## Agents

### 1. Coordinator Agent (Hub)

**Purpose:** AI-driven multi-agent workflow orchestration. The LLM decides which agents to call, in what order, and what to ask. Max 5 plan steps, max 6 loop iterations, 5-min per subtask timeout.

**Skills:**

| Skill | Required Role | Description |
|-------|---------------|-------------|
| `orchestrate_task` | `A2A.Coordinator.Orchestrate` | Accepts a complex task, plans and executes a dynamic multi-agent workflow |

### 2. Planner Agent

**Purpose:** Breaks down complex tasks into actionable steps and suggests appropriate agents.

**Skills:**

| Skill | Required Role | Description |
|-------|---------------|-------------|
| `create_plan` | `A2A.Planner.CreatePlan` | Creates structured plan by breaking down complex tasks into steps |
| `assess_complexity` | `A2A.Planner.AssessComplexity` | Evaluates task complexity and determines required capabilities |
| `suggest_agents` | `A2A.Planner.SuggestAgents` | Recommends which specialist agents should handle parts of a task |

**Inter-Agent Calls:**
- Calls **Research Agent** to gather context before planning
- Calls **Reviewer Agent** to validate generated plans

### 3. Reviewer Agent

**Purpose:** Reviews and validates content, code, plans, and responses for quality assurance.

**Skills:**

| Skill | Required Role | Description |
|-------|---------------|-------------|
| `review_response` | `A2A.Reviewer.ReviewResponse` | Reviews response for quality, accuracy, and completeness |
| `review_code` | `A2A.Reviewer.ReviewCode` | Deep code review analyzing security, performance, and best practices |
| `review_plan` | `A2A.Reviewer.ReviewPlan` | Reviews task plans for completeness, feasibility, and proper agent assignments |
| `provide_feedback` | `A2A.Reviewer.ProvideFeedback` | Provides detailed feedback to other agents about their work output |

### 4. Research Agent

**Purpose:** Gathers information, searches knowledge bases, and provides context.

**Skills:**

| Skill | Required Role | Description |
|-------|---------------|-------------|
| `search_knowledge` | `A2A.Research.SearchKnowledge` | Searches knowledge base and synthesizes relevant information |
| `get_topic_details` | `A2A.Research.GetTopicDetails` | Retrieves comprehensive details about a specific topic |
| `gather_context` | `A2A.Research.GatherContext` | Gathers context from multiple topics for complex tasks |
| `suggest_topics` | `A2A.Research.SuggestTopics` | Suggests related research topics based on a query |

### 5. Code Agent

**Purpose:** Code generation, execution, analysis, and testing.

**Skills:**

| Skill | Required Role | Description |
|-------|---------------|-------------|
| `execute_csharp` | `A2A.Code.ExecuteCsharp` | Executes C# code in a sandboxed environment with timeout protection |
| `generate_code` | `A2A.Code.GenerateCode` | Generates code based on requirements using AI |
| `analyze_code` | `A2A.Code.AnalyzeCode` | Analyzes code structure, complexity, and patterns |
| `generate_tests` | `A2A.Code.GenerateTests` | Generates unit tests for code |
| `refactor_code` | `A2A.Code.RefactorCode` | Refactors code for improved readability, performance, or modularity |

**Inter-Agent Calls:**
- Calls **Reviewer Agent** for code review feedback

## Security

### Authentication & Authorization

All A2A endpoints (except agent card discovery) require JWT Bearer authentication with Azure AD App Roles.

```
POST /a2a/message:send
  │
  ├─ 1. JWT Bearer validation (accessRole on endpoint)
  │
  ├─ 2. Per-skill authorization (ISkillAuthorizationProvider)
  │     server is ISkillAuthorizationProvider?
  │       → ResolveSkill(message) → skill ID
  │       → GetSkillRoles()[skillId] → required role
  │       → User.IsInRole(role)? Allow : 403 Forbid
  │
  └─ 3. Per-user rate limiting (20 req/min, by oid claim)
         Exceeded → 429 Too Many Requests
```

Each agent's `Program.cs` is ~10 lines using shared extensions:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddBackendAuthentication();
builder.AddOlamaSharpClient(OllamaModels.ChatModel);
builder.AddA2AServices();          // Known agents, HTTP clients, rate limiting
builder.AddA2AServer<PlannerA2AServer>();

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseBackendAuthentication();
app.MapA2AEndpoints<PlannerA2AServer>(AuthRoles.A2APlannerAccess);
await app.RunAsync();
```

### Per-Skill Authorization

Each agent overrides `ResolveSkill()` (switch expression on message text) and `GetSkillRoles()` (from `AuthRoles.A2ASkillRoles`). The `MapA2AEndpoints` method checks `ISkillAuthorizationProvider` via `IsSkillForbidden()` — a single boolean expression using `is` pattern matching.

### Rate Limiting

Per-user rate limiting partitions by `oid` JWT claim → IP fallback → "anonymous".

```json
{ "A2A": { "RateLimit": { "PermitLimit": 20, "WindowSeconds": 60, "QueueLimit": 5 } } }
```

## Service Connectivity

### Ollama Connection

Agents connect to Ollama via the centralized model name in `OllamaModels.ChatModel` (Qwen3 32B):

```csharp
builder.AddOlamaSharpClient(OllamaModels.ChatModel);
```

Registers `Lazy<IOllamaApiClient>` — use `Lazy<>` in constructors, not `IOllamaApiClient` directly.

### Inter-Agent Communication

Agents discover each other via Aspire service discovery:

```csharp
// AppHost.cs
plannerAgent
    .WithEnvironment("A2A__KnownAgents__reviewer", reviewerAgent.GetEndpoint("http"))
    .WithEnvironment("A2A__KnownAgents__research", researchAgent.GetEndpoint("http"));
```

Inside an agent, call another agent:

```csharp
var response = await CallAgentAsync("research", $"Gather context for: {taskDescription}", ct);
```

## Running the Agents

### With Aspire (Recommended)

```bash
dotnet run --project AspireOllama.AppHost
```

All agents start automatically and are registered for service discovery.

## Adding a New Agent

1. Create project in `A2A/` with a class extending `A2AServerBase`
2. Override `GetAgentCard()`, `ProcessMessageAsync()`, `ResolveSkill()`, `GetSkillRoles()`
3. Program.cs uses shared extensions (see above)
4. Add to AppHost, solution, gateway routes, terraform (roles + secrets)
5. Register HTTP client in ApiService via `AddA2AClient`
6. Update Coordinator's planning prompt with new agent skills
7. Add skill roles to `AuthScopes.A2ASkillRoles`

## References

- [A2A Protocol Specification](https://github.com/google/a2a-protocol)
- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [OllamaSharp](https://github.com/awaescher/OllamaSharp)
