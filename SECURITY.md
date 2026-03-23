# Security Architecture

This document describes the authentication and authorization architecture for AspireOllama using YARP as a gateway with OAuth2/OIDC flows.

## Overview

YARP (Yet Another Reverse Proxy) acts as the central gateway, routing to all backend services. All backend services share a single audience (`api://{clientId}`) and use JWT Bearer validation with App Roles.

- **Web Frontend**: Authorization Code Flow with PKCE (user authentication via OIDC)
- **Backend Services**: JWT Bearer validation (API, MCP, A2A agents — same audience)
- **Service-to-Service**: OBO token exchange using the shared audience scope

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              YARP Gateway                                   │
│  All routes share the same audience: api://{clientId}                       │
│  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐      │
│  │  /api/*      │ │  /mcp/*      │ │ /a2a/*       │ │  /*          │      │
│  │  → API Svc   │ │  → MCP Svc   │ │ → A2A Agents │ │  → Web UI   │      │
│  └──────┬───────┘ └──────┬───────┘ └──────┬───────┘ └──────┬───────┘      │
└─────────┼────────────────┼────────────────┼────────────────┼──────────────┘
          │                │                │                │
          ▼                ▼                ▼                ▼
   ┌───────────┐   ┌───────────┐   ┌───────────────┐   ┌───────────┐
   │ API Svc   │   │ MCP Svc   │   │ Coordinator   │   │ Web       │
   │ JWT Bearer│   │ JWT Bearer│   │ Planner, etc  │   │ OIDC+OBO  │
   │ Roles     │   │ Roles     │   │ JWT + Roles   │   │           │
   └─────┬─────┘   └───────────┘   └───────────────┘   └─────┬─────┘
         │                                                      │
         └──────────────────────┬───────────────────────────────┘
                                ▼
                    ┌───────────────────┐
                    │   Microsoft       │
                    │   Entra ID        │
                    │   (Azure AD)      │
                    └───────────────────┘
```

## Authentication Flows

### 1. Authorization Code Flow (User Authentication)

The Web frontend authenticates users via OIDC Authorization Code Flow with PKCE. YARP is a pass-through proxy — it does not perform authentication itself.

```
┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐
│  User    │     │   Web    │     │ Entra ID │     │   API    │
│ (Browser)│     │ Frontend │     │(Azure AD)│     │ Service  │
└────┬─────┘     └────┬─────┘     └────┬─────┘     └────┬─────┘
     │                │                │                │
     │  1. Request    │                │                │
     │───────────────▶│                │                │
     │                │                │                │
     │  2. Redirect to Azure AD login  │                │
     │◀───────────────│                │                │
     │                │                │                │
     │  3. Authenticate (PKCE)         │                │
     │────────────────────────────────▶│                │
     │                │                │                │
     │  4. Auth Code  │                │                │
     │◀────────────────────────────────│                │
     │                │                │                │
     │  5. Code → Web │                │                │
     │───────────────▶│                │                │
     │                │  6. Exchange code for tokens    │
     │                │───────────────▶│                │
     │                │  7. Access + Refresh tokens     │
     │                │◀───────────────│                │
     │                │                │                │
     │                │  8. OBO token → API Service     │
     │                │    (via YARP gateway)           │
     │                │───────────────────────────────▶│
     │                │                │                │
     │  9. Response   │                │                │
     │◀───────────────│                │                │
     │                │                │                │
```

### 2. On-Behalf-Of (OBO) Flow (Service-to-Service with User Context)

When a service needs to call another backend service, it acquires a token using the shared audience (`api://{clientId}/.default`). All backend services share the same audience — there are no per-service audiences.

```
┌──────────┐     ┌──────────┐     ┌──────────────┐
│   Web    │     │ Entra ID │     │   Backend    │
│ Frontend │     │ (Azure AD)│     │   Service    │
└────┬─────┘     └────┬─────┘     └──────┬───────┘
     │                │                   │
     │ 1. User signs in (OIDC + PKCE)    │
     │───────────────▶│                   │
     │ 2. Tokens      │                   │
     │◀───────────────│                   │
     │                │                   │
     │ 3. IDownstreamApi OBO exchange     │
     │    (scope: api://{clientId}/.default)
     │───────────────▶│                   │
     │ 4. OBO token   │                   │
     │    (aud: api://{clientId},         │
     │     roles: [...], same user)       │
     │◀───────────────│                   │
     │                │                   │
     │ 5. Call backend with OBO token     │
     │────────────────────────────────────▶
     │                │                   │
     │                │    6. Validate JWT │
     │                │       Check roles │
     │                │                   │
     │ 7. Response    │                   │
     │◀────────────────────────────────────
```

Service-to-service calls (e.g., API → MCP, API → A2A agents) use client credentials with the same shared audience via `ServiceTokenExtensions`.

## Token Audiences and Roles

### App Registrations

| Registration | Purpose | Audience |
|-------------|---------|----------|
| `AspireOllama` (API) | All backend services (API, MCP, A2A agents) | `api://{clientId}` |
| `AspireOllama-Web` | Blazor frontend (OIDC + OBO) | N/A (client only) |

All backend services share the **same audience** (`api://{clientId}`). There is no per-service audience.
The Web app uses a delegated scope `access_as_user` on the API app to perform OBO token exchange.

### Authorization Model

- **RBAC via App Roles** — not scopes. Roles are defined on the API app registration and assigned to groups.
- Roles appear in the `roles` claim of the access token.
- Backend services validate with `RoleClaimType = "roles"` and `Roles("Api.Chat.Write")` on endpoints.
- The Web frontend does NOT check roles from the ID token — it calls `GET /api/me` to read roles from the access token.

### Per-Skill Authorization (A2A Agents)

A2A agents enforce fine-grained, per-skill role checks on `message/send` and `message/stream` endpoints via the `ISkillAuthorizationProvider` interface.

```
┌──────────────────────────────────────────────────────────────────────┐
│                     A2A PER-SKILL AUTHORIZATION                      │
└──────────────────────────────────────────────────────────────────────┘

  Incoming Request (POST /a2a/message:send)
        │
        ▼
  ┌─────────────────────────────────┐
  │ 1. JWT Bearer validation        │  ← accessRole on endpoint
  │    (access token + roles claim) │
  └──────────────┬──────────────────┘
                 │
                 ▼
  ┌─────────────────────────────────┐
  │ 2. IsSkillForbidden()           │  ← checks ISkillAuthorizationProvider
  │                                 │
  │  server is ISkillAuthProvider?  │──── No ──► Allow (skip skill check)
  │         │ Yes                   │
  │         ▼                       │
  │  ResolveSkill(message)          │  ← inspects message text
  │  → skill ID (e.g. "review_code")│
  │         │                       │
  │         ▼                       │
  │  GetSkillRoles()[skillId]       │  ← looks up required role
  │  → "A2A.Reviewer.ReviewCode"   │
  │         │                       │
  │         ▼                       │
  │  User.IsInRole(requiredRole)?   │
  │    Yes → Allow                  │
  │    No  → 403 Forbid             │
  └─────────────────────────────────┘
```

**Skill-to-Role Mapping** (defined in `AuthScopes.cs`):

| Agent | Skill | Required Role |
|-------|-------|---------------|
| Coordinator | `orchestrate_task` | `A2A.Coordinator.Orchestrate` |
| Planner | `create_plan` | `A2A.Planner.CreatePlan` |
| Planner | `assess_complexity` | `A2A.Planner.AssessComplexity` |
| Planner | `suggest_agents` | `A2A.Planner.SuggestAgents` |
| Reviewer | `review_response` | `A2A.Reviewer.ReviewResponse` |
| Reviewer | `review_code` | `A2A.Reviewer.ReviewCode` |
| Reviewer | `review_plan` | `A2A.Reviewer.ReviewPlan` |
| Reviewer | `provide_feedback` | `A2A.Reviewer.ProvideFeedback` |
| Research | `search_knowledge` | `A2A.Research.SearchKnowledge` |
| Research | `get_topic_details` | `A2A.Research.GetTopicDetails` |
| Research | `gather_context` | `A2A.Research.GatherContext` |
| Research | `suggest_topics` | `A2A.Research.SuggestTopics` |
| Code | `execute_csharp` | `A2A.Code.ExecuteCsharp` |
| Code | `generate_code` | `A2A.Code.GenerateCode` |
| Code | `analyze_code` | `A2A.Code.AnalyzeCode` |
| Code | `generate_tests` | `A2A.Code.GenerateTests` |
| Code | `refactor_code` | `A2A.Code.RefactorCode` |

### OBO Token Flow

When the Web frontend calls a backend service:

```
Web (cookie auth) → IDownstreamApi (OBO exchange) → access token for api://{clientId}/.default → API Service
```

The resulting access token contains:
```json
{
  "aud": "api://{clientId}",
  "iss": "https://login.microsoftonline.com/{tenantId}/v2.0",
  "oid": "{user-object-id}",
  "name": "User Name",
  "roles": ["Api.Chat.Write", "Api.Sessions.Manage", "Mcp.Tools.GetTime"],
  "azp": "{web-client-id}"
}
```

### Audience Validation

Backend services accept both `api://{clientId}` and bare `{clientId}` as valid audiences:
```csharp
ValidAudiences = new[] { azureAdOptions.Audience, clientId }
```

## YARP Gateway Configuration

### Route Configuration

The gateway routes are configured in `AspireOllama.Gateway/appsettings.json`. Routes use Aspire service discovery addresses (`https+http://servicename`):

| Route | Path | Cluster | Timeout |
|-------|------|---------|---------|
| `api-route` | `/api/{**catch-all}` | apiservice | 15 min |
| `mcp-route` | `/mcp/{**catch-all}` | mcpserver | 10 min |
| `a2a-coordinator-route` | `/a2a/coordinator/{**catch-all}` | coordinator-agent | 15 min |
| `a2a-planner-route` | `/a2a/planner/{**catch-all}` | planner-agent | 10 min |
| `a2a-reviewer-route` | `/a2a/reviewer/{**catch-all}` | reviewer-agent | 10 min |
| `a2a-research-route` | `/a2a/research/{**catch-all}` | research-agent | 10 min |
| `a2a-code-route` | `/a2a/code/{**catch-all}` | code-agent | 10 min |
| `scalar-route` | `/scalar/{**catch-all}` | scalar | — |
| `web-route` | `/{**catch-all}` | webfrontend | — |

Each backend service validates JWT tokens independently. The gateway itself does not perform authentication — it passes tokens through to the downstream services.

## Identity Provider Setup (Entra ID)

Managed by Terraform in `infra/terraform/`. Two app registrations only.

### App Registrations

| Registration | Purpose | Type |
|-------------|---------|------|
| `AspireOllama` | All backend services (API, MCP, A2A agents) | Web API (Resource Server) |
| `AspireOllama-Web` | Blazor frontend | Web App (Confidential Client) |

### 1. AspireOllama (API — Resource Server)

```
Application ID URI: api://{clientId}
Delegated Scope: access_as_user (enables OBO)
App Roles: 32 roles across Api, Mcp, A2A agents (see AuthScopes.cs)
Token version: v2.0
```

All backend services validate tokens against this single audience.
App roles are assigned to security groups (Viewer, Standard User, Power User, Admin).

### 2. AspireOllama-Web (Frontend — Confidential Client)

```
Redirect URIs: https://localhost:7200/signin-oidc, https://ai.ranjeeth.us/signin-oidc
Required Permissions: access_as_user scope on AspireOllama API
Auth flow: Authorization Code + PKCE → OBO for downstream calls
```

No app roles are defined on this registration — roles come from the API's access token.

## Implementation Components

### YARP Gateway Project Structure

```
AspireOllama.Gateway/
├── Program.cs                 # Gateway configuration
├── appsettings.json          # YARP routes + cluster config
├── appsettings.Docker.json   # Docker-specific overrides
└── AspireOllama.Gateway.csproj
```

### Key NuGet Packages

| Package | Purpose |
|---------|---------|
| `Yarp.ReverseProxy` | Reverse proxy functionality |
| `LettuceEncrypt` | Let's Encrypt TLS certificates |
| `Microsoft.Extensions.ServiceDiscovery.Yarp` | Aspire service discovery integration |

### Token Propagation

The gateway passes tokens through to backend services via standard YARP proxy behavior. OBO token exchange is handled by the Web frontend (`IDownstreamApi`) and the API service (`ServiceTokenExtensions`), not by the gateway itself.

## MCP Server Authentication

### Configuration

The MCP server uses `AddBackendAuthentication()` for JWT Bearer validation (same as all backend services). Per-tool role enforcement is handled by `McpToolRoleMiddleware`, which inspects `tools/call` requests and checks the user's roles against the `McpToolRoles` mapping in `AuthScopes.cs`.

```csharp
// MCP Server Program.cs
builder.AddServiceDefaults();
builder.AddBackendAuthentication();
builder.Services.AddMcpServer(...).WithHttpTransport().WithToolsFromAssembly();

app.UseBackendAuthentication();
app.UseMiddleware<McpToolRoleMiddleware>();  // Per-tool RBAC
app.MapMcp("/mcp");
```

## Security Considerations

### Token Caching

- MSAL token cache uses **Redis** via `AddDistributedTokenCaches()` (replaces in-memory)
- Redis deployed automatically by Aspire via `builder.AddRedis("redis")`
- Web frontend registers `AddRedisDistributedCache("redis")` for the `IDistributedCache` backing store
- Tokens survive restarts and are shared across multiple instances
- Token refresh handled automatically before expiration
- OBO tokens cached per user + scope combination
- `ChatApiClient.EnsureAuthenticatedAsync()` checks auth state before calling MSAL, redirecting to sign-in if unauthenticated (prevents `user_null` exceptions on circuit reconnect)

### Rate Limiting

Per-user rate limiting is applied on all A2A endpoints (except agent card discovery) via `A2ARateLimiting.cs`. Partitions by `oid` JWT claim → IP fallback → "anonymous". Configuration:

```json
{ "A2A": { "RateLimit": { "PermitLimit": 20, "WindowSeconds": 60, "QueueLimit": 5 } } }
```

Returns 429 Too Many Requests when exceeded.

## Deployment Checklist

- [ ] Register applications in Entra ID (see `infra/terraform/`)
- [ ] Configure app secrets in Key Vault or environment-specific secrets
- [ ] Set up redirect URIs for each environment
- [ ] Configure CORS policies
- [ ] Enable HTTPS everywhere
- [x] Configure token caching (Redis) — deployed via Aspire, uses `AddDistributedTokenCaches()`
- [x] Configure observability (New Relic) — OTLP export with service names and resource attributes
- [x] Replace SQLite with MongoDB — chat persistence with native document storage
- [x] Add Qdrant vector database — RAG document storage with dot product similarity
- [x] Role-based document upload — `Api.Admin` / `Api.Documents.Manage` on access token
- [x] User profile from access token — `GET /api/me` returns roles, cached by `UserRoleService`
- [x] User sessions scoped by userId — MongoDB queries filter by userId from access token
- [x] Consistent timeouts — 15 minutes for resilience handler and HTTP clients, gateway: 15 min for API/Coordinator, 10 min for other agents/MCP
- [x] Heartbeat logging suppressed — `ServiceDefaults` filters health-check logs to Warning level
- [x] Coordinator Agent — AI-driven planning, parallel execution, result aggregation, knows all 17 skills across 4 agents
- [x] Dual-model architecture — Qwen3 (32B) for text/tools, Qwen2.5-VL (32B) for vision, model names centralized in `OllamaModels.cs`
- [x] RAG as a tool — `search_knowledge_base` with relevance scores and `top_k` parameter
- [x] Image analysis as a tool — `analyze_image` retrieves from session history
- [x] Calculator tool — built-in math evaluation
- [x] Workflow UI — hub diagram, expandable blocks, preset buttons, Call Summary, Final Result with markdown rendering
- [x] Chat UI — markdown rendering (Markdig) for agent responses, auto-scroll, full-width message layout
- [x] Web search tool — SerpAPI integration for real-time Google search (key in appsettings.Secrets.json)
- [ ] Set up audit logging
- [x] Per-user rate limiting — A2A agents: 20 req/min per user (oid claim), 429 when exceeded, built into A2AHostExtensions
- [x] Per-skill authorization — `ISkillAuthorizationProvider` interface, each agent maps messages to skills and skill roles from `AuthScopes.A2ASkillRoles`, enforced on `message/send` and `message/stream`, returns 403 Forbid
- [ ] Test OBO flow end-to-end
- [ ] Verify tool scope restrictions
- [ ] Deploy to production (Docker or Kubernetes)
