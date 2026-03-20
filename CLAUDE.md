# CLAUDE.md — Project Guide for AI Assistants

## Project Overview

AspireOllama is a .NET Aspire 13 application with local LLM inference (Ollama), multi-agent A2A protocol, RAG (Qdrant), MongoDB persistence, and a Blazor Server frontend.

## Build & Run

```bash
dotnet build                              # Build full solution
dotnet run --project AspireOllama.AppHost  # Run via Aspire orchestrator
```

The app is locked by running processes — stop the app before rebuilding if you get MSB3027 file-lock errors.

## Architecture

- **Two LLM models**: Qwen3 (32B) for text/tools, Qwen2.5-VL (32B) for vision. Model names centralized in `AspireOllama.Shared/OllamaModels.cs`.
- **Single persona**: User sees one AI assistant. AiChatService routes images to Qwen2.5-VL via `VisionTool`, text to Qwen3 with tools.
- **Tools registered via IToolRegistry** in `Services/Tools/`. Every tool method takes `session_id` as first parameter. AiChatService gets tools from the registry — no inline tool definitions.
- **RAG**: Documents uploaded via `/documents` page → chunked → embedded (nomic-embed-text) → stored in Qdrant. Chat searches via `RagSearchTool` with dot product similarity.
- **MongoDB**: Chat sessions scoped by `userId` from access token. No SQLite.
- **A2A Protocol**: 5 agents (Coordinator, Planner, Reviewer, Research, Code). Coordinator is the hub — AI-driven planning, parallel execution, aggregation.
- **Auth**: Azure AD with OIDC (frontend), OBO (service-to-service), JWT Bearer (backend). Roles checked on access token, not ID token. `UserRoleService` caches roles from `GET /api/me`.

## Key Conventions

- **Secrets in `appsettings.Secrets.json`** (gitignored). Never put keys in `appsettings.json`.
- **All timeouts: 15 minutes** across gateway, resilience handler, HTTP clients. Coordinator subtask: 5 min. Max plan steps: 5.
- **Minimal retries** (`MaxRetryAttempts = 1`). AI requests are expensive. Minimum allowed by resilience handler is 1.
- **Prerender disabled** on Blazor pages (`prerender: false`). Cross-page navigation uses `forceLoad: true`.
- **Heartbeat logging suppressed** in ServiceDefaults. Only warnings logged for health checks.
- **Terraform** in `infra/terraform/` manages Azure AD app registrations, roles, and secrets generation.

## Project Structure

```
AspireOllama.AppHost/          — Aspire orchestrator (MongoDB, Qdrant, Redis, Ollama, Gateway)
AspireOllama.ApiService/       — REST API, tools, RAG, A2A coordination
  Services/AI/AiChatService.cs — Main chat logic, routes to tools via IToolRegistry
  Services/Tools/              — All tools: Calculator, WebSearch, RagSearch, Vision, CodeExecution, FileOps
  Services/Rag/                — Qdrant vector search
  Services/Embedding/          — Ollama embedding service
  Services/Document/           — Text extraction, chunking, ingestion
  Services/Session/            — MongoDB session CRUD (scoped by userId)
  Services/Message/            — MongoDB message CRUD
  Endpoints/                   — FastEndpoints (prefixed /api/)
  Data/                        — MongoDB documents, collections, index initializer
AspireOllama.Web/              — Blazor Server frontend
  ChatApiClient.cs             — Calls API via IDownstreamApi (OBO)
  UserRoleService.cs           — Caches roles from access token
  Components/Pages/Chat.razor  — Main chat UI
  Components/Pages/Agents.razor — A2A workflow UI with hub diagram
  Components/Pages/Documents.razor — RAG document upload (admin)
AspireOllama.Shared/           — DTOs, OllamaModels.cs, AgentModels
AspireOllama.ServiceDefaults/  — Auth, OpenTelemetry, resilience, health checks
AspireOllama.Gateway/          — YARP reverse proxy, Let's Encrypt
A2A/
  AspireOllama.A2A.CoordinatorAgent/ — Workflow orchestrator (plans, dispatches, aggregates)
  AspireOllama.A2A.PlannerAgent/     — create_plan, assess_complexity, suggest_agents
  AspireOllama.A2A.ReviewerAgent/    — review_response, review_code, review_plan, provide_feedback
  AspireOllama.A2A.ResearchAgent/    — search_knowledge, get_topic_details, gather_context, suggest_topics
  AspireOllama.A2A.CodeAgent/        — execute_csharp, generate_code, analyze_code, generate_tests, refactor_code
  AspireOllama.A2A.Shared/           — A2AServerBase, A2AAgentClient, protocol models
```

## Common Patterns

### Adding a new tool
1. Create `MyTool.cs` in `Services/Tools/` implementing `ITool`
2. Method signature: `public async Task<string> DoSomethingAsync(string session_id, ...)`
3. Register in `Program.cs`: `builder.Services.AddSingleton<MyTool>();`
4. Add to `ToolRegistry` constructor and register via `AIFunctionFactory.Create`
5. Update system prompt in `AiChatService` to mention the tool

### Adding a new A2A agent
1. Create project in `A2A/` following PlannerAgent pattern
2. Add to AppHost: project, agent discovery env vars, Ollama reference, gateway, Scalar, telemetry
3. Add to solution (.slnx), gateway routes (appsettings.json), terraform (roles + secrets)
4. Register HTTP client in ApiService Program.cs via `AddA2AClient`
5. Update Coordinator's planning prompt with new agent skills

### Changing LLM models
Edit `AspireOllama.Shared/OllamaModels.cs` — one file changes all services and agents.

## Infrastructure

| Service | Purpose |
|---------|---------|
| MongoDB | Chat sessions + messages (scoped by userId) |
| Qdrant | RAG vector store (dot product, nomic-embed-text embeddings) |
| Redis | MSAL distributed token cache |
| Ollama | LLM inference (Qwen3, Qwen2.5-VL, nomic-embed-text) |
| YARP Gateway | Reverse proxy, Let's Encrypt TLS |

## Auth Flow

```
User → OIDC (Azure AD) → Cookie → Web Frontend
Web → OBO token → API Service (JWT Bearer, roles from access token)
API → OBO token → A2A Agents (JWT Bearer)
```

Roles checked on access token only. ID token for identification. `[Authorize(Roles)]` on Blazor pages does NOT work (no roles in ID token). Use `UserRoleService` which calls `GET /api/me` to get roles from the access token.

## MCP Server

The MCP (Model Context Protocol) server exposes tools via HTTP transport at `/mcp`. The API service connects as an MCP client and makes tools available to the LLM.

### Existing MCP Tools
- `get_time(timezone)` — current time in a timezone/city
- `get_weather(city)` — weather for a city (demo data)
- `convert_units(value, from_unit, to_unit)` — unit conversion (km/miles, kg/lbs, celsius/fahrenheit)

### Adding a new MCP tool
1. Create `MyTool.cs` in `AspireOllama.McpServer/Tools/`
2. Use the `[McpServerToolType]` attribute on the class and `[McpServerTool]` on the method:
```csharp
[McpServerToolType]
public static class MyTool
{
    [McpServerTool, Description("Does something useful")]
    public static string my_tool(
        [Description("Parameter description")] string param)
    {
        return "result";
    }
}
```
3. Tools are auto-discovered via `.WithToolsFromAssembly()` in Program.cs — no manual registration needed.
4. Add RBAC role mapping in `AuthScopes.cs`:
```csharp
public static readonly Dictionary<string, string> McpToolRoles = new()
{
    ["my_tool"] = McpToolsMyTool,  // Add constant + mapping
};
```
5. The `McpToolRoleMiddleware` enforces per-tool role checks on `tools/call` requests.

### MCP Client (API Service)
- Registered via `builder.AddMcpServerClient()` in ApiService Program.cs
- Uses OBO token propagation for auth
- Connects at startup via `McpService.InitializeAsync()`
- Gateway routes `/mcp/*` to the MCP server

## Web Search Tool

Uses SerpAPI (Google results). Config in `appsettings.json`:
```json
{
  "Tools": {
    "EnableWebSearch": true,
    "WebSearch": { "SerpApiKey": "" }
  }
}
```
Put the actual key in `appsettings.Secrets.json`. Free tier: 100 searches/month at [serpapi.com](https://serpapi.com/).

## Workflow UI

The Agents page (`/agents`) has a multi-agent workflow tab:
- **Preset buttons**: 4 one-click workflow templates (Full Stack Auth, REST API, Security Audit, Refactoring)
- **Hub diagram**: Coordinator plan → Agents box → Coordinator aggregate, with expandable [+] blocks
- **Call Summary**: collapsible bar with per-agent stats
- **Final Result**: collapsible, rendered as **markdown** (Markdig), with "Show all" / "Show less" toggle (300px compact ↔ full height)
- Code blocks in final result get dark background, monospace font, proper formatting

## `AddTextArtifact` Parameter Order

The base class `A2AServerBase.AddTextArtifact` signature is `(task, text, name)` — text first, name second. This has caused bugs before. Always verify parameter order when calling it.

## System Prompt

`AiChatService.SystemPrompt` is a `static string` property (not `const`) because it includes `DateTime.UtcNow` for today's date. The prompt instructs the LLM to:
- Call `web_search` for current events (LLM has knowledge cutoff)
- NOT search RAG when images or documents are uploaded inline
- Pass `session_id` to every tool call
- Only use RAG results with relevance > 0.6

## Gotchas

- **Aspire AppHost project references** are for orchestration, not compile dependencies. Use `IsAspireProjectResource="false"` for library references (e.g., Shared).
- **OllamaSharp registers `Lazy<IOllamaApiClient>`**, not `IOllamaApiClient`. Use `Lazy<>` in constructors.
- **Blazor Server + prerender**: causes double-render. All pages use `prerender: false`.
- **Large file upload**: goes through `/upload-documents` minimal API endpoint (not SignalR). Uses `IDownstreamApi` with `HttpContext.User`, not `AuthenticationStateProvider`.
- **Gateway body size**: 100MB for document uploads. All clusters have 15-min `ActivityTimeout`.
- **Coordinator workflow**: max 5 plan steps, max 6 loop iterations, 5-min per subtask timeout. No conflict resolution re-runs. Reviewer feedback goes to aggregation instead.
- **`AddTextArtifact(task, text, name)`**: text is second param, name is third. Easy to swap — verify order.
- **Resilience handler `MaxRetryAttempts`**: minimum is 1, not 0. Setting 0 throws `OptionsValidationException`.
- **SerpAPI for web search** (not Google Custom Search which requires billing). Key in `appsettings.Secrets.json`.
- **Markdig** renders Final Result as HTML. Uses default pipeline (no `UseAdvancedExtensions` — removed in Markdig 1.1.1).
