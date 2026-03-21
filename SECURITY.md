# Security Architecture

This document describes the authentication and authorization architecture for AspireOllama using YARP as a gateway with OAuth2/OIDC flows.

## Overview

YARP (Yet Another Reverse Proxy) acts as the central gateway, exposing both API Service and MCP Server with different authentication configurations:

- **API Service**: Authorization Code Flow (user identity)
- **MCP Server**: On-Behalf-Of (OBO) Flow (user context preserved for tool scope)

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

Used for user-facing API endpoints. The user authenticates interactively and receives an access token.

```
┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐
│  User    │     │  YARP    │     │ Identity │     │   API    │
│ (Browser)│     │ Gateway  │     │ Provider │     │ Service  │
└────┬─────┘     └────┬─────┘     └────┬─────┘     └────┬─────┘
     │                │                │                │
     │  1. Request    │                │                │
     │───────────────▶│                │                │
     │                │                │                │
     │  2. Redirect to login           │                │
     │◀───────────────│                │                │
     │                │                │                │
     │  3. Authenticate                │                │
     │────────────────────────────────▶│                │
     │                │                │                │
     │  4. Auth Code  │                │                │
     │◀────────────────────────────────│                │
     │                │                │                │
     │  5. Code + Request              │                │
     │───────────────▶│                │                │
     │                │  6. Exchange code for tokens    │
     │                │───────────────▶│                │
     │                │  7. Access + Refresh tokens     │
     │                │◀───────────────│                │
     │                │                │                │
     │                │  8. Forward with Bearer token   │
     │                │───────────────────────────────▶│
     │                │                │                │
     │  9. Response   │                │                │
     │◀───────────────│                │                │
     │                │                │                │
```

### 2. On-Behalf-Of (OBO) Flow (Service-to-Service with User Context)

When API Service needs to call MCP Server, it exchanges the user's token for a new token scoped to MCP Server. This preserves user identity for tool authorization.

```
┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐
│   API    │     │ Identity │     │   YARP   │     │   MCP    │
│ Service  │     │ Provider │     │ Gateway  │     │  Server  │
└────┬─────┘     └────┬─────┘     └────┬─────┘     └────┬─────┘
     │                │                │                │
     │ 1. User request with token      │                │
     │ (needs MCP tool)                │                │
     │                │                │                │
     │ 2. OBO Token Request            │                │
     │ (assertion: user_token,         │                │
     │  scope: api://mcp-server/.default)               │
     │───────────────▶│                │                │
     │                │                │                │
     │ 3. OBO Access Token             │                │
     │ (audience: mcp-server,          │                │
     │  subject: original user)        │                │
     │◀───────────────│                │                │
     │                │                │                │
     │ 4. Call MCP with OBO token      │                │
     │────────────────────────────────▶│                │
     │                │                │ 5. Forward     │
     │                │                │───────────────▶│
     │                │                │                │
     │                │                │ 6. Validate    │
     │                │                │    token       │
     │                │                │    (user ctx)  │
     │                │                │                │
     │ 7. Tool result │                │                │
     │◀────────────────────────────────────────────────│
     │                │                │                │
```

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

```json
{
  "ReverseProxy": {
    "Routes": {
      "api-route": {
        "ClusterId": "api-cluster",
        "AuthorizationPolicy": "AuthCodePolicy",
        "Match": {
          "Path": "/api/{**catch-all}"
        },
        "Transforms": [
          { "PathRemovePrefix": "/api" }
        ]
      },
      "chat-route": {
        "ClusterId": "api-cluster",
        "AuthorizationPolicy": "AuthCodePolicy",
        "Match": {
          "Path": "/chat"
        }
      },
      "sessions-route": {
        "ClusterId": "api-cluster",
        "AuthorizationPolicy": "AuthCodePolicy",
        "Match": {
          "Path": "/sessions/{**catch-all}"
        }
      },
      "mcp-route": {
        "ClusterId": "mcp-cluster",
        "AuthorizationPolicy": "OboPolicy",
        "Match": {
          "Path": "/mcp/{**catch-all}"
        },
        "Transforms": [
          { "PathRemovePrefix": "/mcp" }
        ]
      }
    },
    "Clusters": {
      "api-cluster": {
        "Destinations": {
          "api": {
            "Address": "https+http://apiservice"
          }
        }
      },
      "mcp-cluster": {
        "Destinations": {
          "mcp": {
            "Address": "https+http://mcpserver"
          }
        }
      }
    }
  }
}
```

### Authentication Configuration

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "{tenant-id}",
    "ClientId": "{gateway-client-id}",
    "ClientSecret": "{gateway-client-secret}",
    "CallbackPath": "/signin-oidc",
    "SignedOutCallbackPath": "/signout-callback-oidc"
  },
  "DownstreamApis": {
    "McpServer": {
      "Scopes": ["api://aspireollama-mcp/.default"]
    }
  }
}
```

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
App Roles: 31 roles across Api, Mcp, A2A agents (see AuthScopes.cs)
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
├── appsettings.json          # YARP routes + auth config
├── Transforms/
│   └── OboTokenTransform.cs  # Injects OBO token for MCP routes
└── AspireOllama.Gateway.csproj
```

### Key NuGet Packages

| Package | Purpose |
|---------|---------|
| `Yarp.ReverseProxy` | Reverse proxy functionality |
| `Microsoft.Identity.Web` | Entra ID authentication |
| `Microsoft.Identity.Web.TokenAcquisition` | OBO token acquisition |

### OBO Token Transform

```csharp
public class OboTokenTransform : ITransformProvider
{
    public void Apply(TransformBuilderContext context)
    {
        if (context.Route.RouteId == "mcp-route")
        {
            context.AddRequestTransform(async transformContext =>
            {
                var tokenAcquisition = transformContext.HttpContext
                    .RequestServices.GetRequiredService<ITokenAcquisition>();

                var token = await tokenAcquisition.GetAccessTokenForUserAsync(
                    scopes: ["api://aspireollama-mcp/.default"],
                    authenticationScheme: OpenIdConnectDefaults.AuthenticationScheme);

                transformContext.ProxyRequest.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            });
        }
    }
}
```

## MCP Server Authentication

### JWT Bearer Configuration

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ToolsExecute", policy =>
        policy.RequireScope("Tools.Execute"));
});
```

### Tool Authorization

MCP tools can access user context from the validated token:

```csharp
[McpServerToolType]
public class SecureWeatherTool
{
    [McpServerTool("get_weather")]
    [Description("Get weather for a city (requires Tools.Execute scope)")]
    public string GetWeather(
        [Description("City name")] string city,
        HttpContext httpContext)
    {
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var userName = httpContext.User.FindFirst("name")?.Value;

        // Log or audit tool usage per user
        // Apply user-specific tool restrictions

        return $"Weather for {city}: 22°C, Sunny (requested by {userName})";
    }
}
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

Configure per-user rate limits on tool execution:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("ToolRateLimit", context =>
        RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: context.User.Identity?.Name ?? "anonymous",
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 100,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                TokensPerPeriod = 10
            }));
});
```

### Audit Logging

All tool executions are logged with user context:

```json
{
  "timestamp": "2024-01-15T10:30:00Z",
  "userId": "user@example.com",
  "toolName": "get_weather",
  "arguments": { "city": "London" },
  "result": "success",
  "executionTimeMs": 45
}
```

## Development Configuration

For local development without full identity provider setup:

```json
{
  "Authentication": {
    "Development": {
      "Enabled": true,
      "DefaultUser": "dev@localhost",
      "DefaultScopes": ["Chat.Read", "Chat.Write", "Tools.Execute"]
    }
  }
}
```

## Deployment Checklist

- [ ] Register applications in Entra ID (see `infra/terraform/`)
- [ ] Configure app secrets in Key Vault or K8s Secrets (`k8s/base/secrets.yaml`)
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
- [x] Consistent timeouts — 10 minutes across all services, 15 minutes for gateway to coordinator/apiservice
- [x] Heartbeat logging suppressed — `ServiceDefaults` filters health-check logs to Warning level
- [x] Coordinator Agent — AI-driven planning, parallel execution, conflict resolution, knows all 17 skills across 4 agents
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
- [ ] Deploy to Kubernetes (`k8s/base/`) with nginx Ingress replacing YARP gateway
