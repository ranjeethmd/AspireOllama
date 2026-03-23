# Azure AD Setup for AspireOllama

## Overview

| App Registration | Purpose |
|---|---|
| **AspireOllama** | API resource server — exposes delegated scope for OBO consent, defines App Roles for authorization |
| **AspireOllama-Web** | Blazor frontend — authenticates users via Authorization Code Flow with PKCE, acquires OBO tokens |

**Authentication**: Authorization Code Flow with PKCE (Web) + JWT Bearer (backend services)
**Token consent**: Delegated scope `access_as_user` on the API app enables OBO token acquisition
**Authorization**: Azure AD App Roles (RBAC) — the `roles` claim in the token, enforced via `[Authorize(Roles = "...")]`

```
User signs in via Authorization Code Flow with PKCE
  → AspireOllama-Web requests scope: api://aspireollama/access_as_user
  → Azure AD returns token with:
      - aud: api://aspireollama
      - scp: access_as_user (delegated scope for OBO consent)
      - roles: [Api.Chat.Read, Api.Chat.Write, ...] (App Roles assigned to user)
  → Aspire YARP Gateway routes request to backend service
  → API Service validates token and checks roles → Roles("Api.Chat.Write")
  → MCP Server checks roles → User.IsInRole("Mcp.Tools.GetTime")
  → A2A Agents check roles → [Authorize(Roles = "A2A.Planner.Access")]
  → Agent-to-Agent: OBO token propagated, same roles enforced
```

---

## 1. Create App Registration: `AspireOllama`

**Azure Portal → Azure Active Directory → App registrations → New registration**

| Setting | Value |
|---|---|
| Name | `AspireOllama` |
| Supported account types | Accounts in this organizational directory only (Single tenant) |
| Redirect URI | _(leave blank)_ |

After creation:

1. **Expose an API → Set** Application ID URI to `api://aspireollama`
2. **Authentication** → Do NOT enable implicit grant (we use Authorization Code Flow with PKCE)
3. **Certificates & secrets → New client secret** → Copy the value (store in Key Vault or User Secrets)

---

## 2. Expose a Delegated Scope on `AspireOllama`

**Azure Portal → AspireOllama → Expose an API → Add a scope**

This scope enables the Web app to acquire OBO tokens on behalf of the user. It does **not** control authorization — that is handled by App Roles.

| Setting | Value |
|---|---|
| Scope name | `access_as_user` |
| Who can consent? | Admins only |
| Admin consent display name | Access AspireOllama as signed-in user |
| Admin consent description | Allows the app to access AspireOllama APIs on behalf of the signed-in user |
| User consent display name | Access AspireOllama on your behalf |
| User consent description | Allows the app to access AspireOllama APIs on your behalf |
| State | Enabled |

The full scope URI will be: `api://aspireollama/access_as_user`

---

## 3. Create App Roles on `AspireOllama`

**Azure Portal → AspireOllama → App roles → Create app role**

For each role below:
- **Allowed member types**: Users/Groups
- **Do you want to enable this app role?**: Yes

### API Service Roles

| Display Name | Value | Description |
|---|---|---|
| API Access | `Api.Access` | Coarse access to the API Service |
| Admin | `Api.Admin` | Admin access (document upload, management) |
| Read Chat | `Api.Chat.Read` | Read chat messages and session history |
| Write Chat | `Api.Chat.Write` | Send chat messages and call agents |
| Manage Sessions | `Api.Sessions.Manage` | Create and delete chat sessions |
| Manage Documents | `Api.Documents.Manage` | Upload documents to RAG knowledge base |

### MCP Server Roles

| Display Name | Value | Description |
|---|---|---|
| MCP Access | `Mcp.Access` | Coarse access to MCP tools |
| Get Time | `Mcp.Tools.GetTime` | Use the time tool |
| Get Weather | `Mcp.Tools.GetWeather` | Use the weather tool |
| Convert Units | `Mcp.Tools.ConvertUnits` | Use the unit conversion tool |

### Coordinator Agent Roles

| Display Name | Value | Description |
|---|---|---|
| Coordinator Access | `A2A.Coordinator.Access` | Coarse access to Coordinator agent |
| Orchestrate Task | `A2A.Coordinator.Orchestrate` | Run multi-agent workflows |

### Planner Agent Roles

| Display Name | Value | Description |
|---|---|---|
| Planner Access | `A2A.Planner.Access` | Coarse access to Planner agent |
| Create Plan | `A2A.Planner.CreatePlan` | Create task plans |
| Assess Complexity | `A2A.Planner.AssessComplexity` | Assess task complexity |
| Suggest Agents | `A2A.Planner.SuggestAgents` | Get agent suggestions for a task |

<!-- REVIEWER: Terraform (main.tf) still defines `A2A.Planner.OrchestrateWorkflow` as a Planner role,
     but it is NOT enforced in AuthScopes.cs. Orchestration is the Coordinator's responsibility
     (A2A.Coordinator.Orchestrate). Consider removing the orphaned role from Terraform. -->

### Reviewer Agent Roles

| Display Name | Value | Description |
|---|---|---|
| Reviewer Access | `A2A.Reviewer.Access` | Coarse access to Reviewer agent |
| Review Response | `A2A.Reviewer.ReviewResponse` | Review AI responses |
| Review Code | `A2A.Reviewer.ReviewCode` | Review code |
| Review Plan | `A2A.Reviewer.ReviewPlan` | Review plans |
| Provide Feedback | `A2A.Reviewer.ProvideFeedback` | Get review feedback |

### Research Agent Roles

| Display Name | Value | Description |
|---|---|---|
| Research Access | `A2A.Research.Access` | Coarse access to Research agent |
| Search Knowledge | `A2A.Research.SearchKnowledge` | Search knowledge base |
| Get Topic Details | `A2A.Research.GetTopicDetails` | Get detailed topic info |
| Gather Context | `A2A.Research.GatherContext` | Gather research context |
| Suggest Topics | `A2A.Research.SuggestTopics` | Get topic suggestions |

### Code Agent Roles

| Display Name | Value | Description |
|---|---|---|
| Code Access | `A2A.Code.Access` | Coarse access to Code agent |
| Execute C# | `A2A.Code.ExecuteCsharp` | Execute C# code |
| Generate Code | `A2A.Code.GenerateCode` | Generate code |
| Analyze Code | `A2A.Code.AnalyzeCode` | Analyze code |
| Generate Tests | `A2A.Code.GenerateTests` | Generate test code |
| Refactor Code | `A2A.Code.RefactorCode` | Refactor code |

**Total: 32 App Roles + 1 Delegated Scope**

---

## 4. Create App Registration: `AspireOllama-Web`

**Azure Portal → Azure Active Directory → App registrations → New registration**

| Setting | Value |
|---|---|
| Name | `AspireOllama-Web` |
| Supported account types | Accounts in this organizational directory only (Single tenant) |
| Redirect URI | Web — `https://localhost:7000/signin-oidc` |

After creation:

1. **Authentication → Front-channel logout URL** → `https://localhost:7000/signout-callback-oidc`
2. **Authentication** → Do NOT enable implicit grant (Authorization Code Flow with PKCE is configured in code)
3. **Certificates & secrets → New client secret** → Copy the value

---

## 5. Grant `AspireOllama-Web` Permission to Call `AspireOllama`

**Azure Portal → AspireOllama-Web → API permissions → Add a permission**

1. Select **My APIs → AspireOllama**
2. Select **Delegated permissions**
3. Check `access_as_user`
4. Click **Add permissions**
5. Click **Grant admin consent for [your tenant]**

This allows the Web app to acquire OBO tokens with `audience: api://aspireollama`. The user's assigned App Roles will automatically appear in the token's `roles` claim.

---

## 6. Assign Users to Roles

**Azure Portal → Enterprise Applications → AspireOllama → Users and groups → Add user/group**

1. Select a user or group
2. Select the role(s) to assign
3. Click **Assign**

### Example Role Assignments

| Persona | Assigned Roles |
|---|---|
| **Read-Only Viewer** | `Api.Chat.Read` |
| **Standard User** | `Api.Chat.Read`, `Api.Chat.Write`, `Api.Sessions.Manage`, `Mcp.Access`, `Mcp.Tools.GetTime`, `Mcp.Tools.GetWeather`, `Mcp.Tools.ConvertUnits` |
| **Power User** | All Standard User roles + `A2A.Planner.Access`, `A2A.Reviewer.Access`, `A2A.Research.Access`, `A2A.Code.Access` |
| **Admin** | All 32 roles |

> Note: Azure AD requires a separate assignment per role. For bulk assignment, use security groups — assign roles to the group, then add users to the group.

---

## 7. Application Configuration

### All Backend Services

All backend services (API Service, MCP Server, A2A Agents) share the **AspireOllama** app registration credentials:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<your-tenant-id>",
    "ClientId": "<AspireOllama-client-id>",
    "ClientSecret": "<AspireOllama-client-secret>",
    "Audience": "api://<AspireOllama-client-id>"
  }
}
```

### Web Frontend (Blazor)

Uses the **AspireOllama-Web** app registration credentials:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<your-tenant-id>",
    "ClientId": "<AspireOllama-Web-client-id>",
    "ClientSecret": "<AspireOllama-Web-client-secret>",
    "Audience": "api://<AspireOllama-client-id>"
  }
}
```

> **Never commit secrets to source control.** Use `dotnet user-secrets` for local development or Azure Key Vault for deployed environments.

---

## 8. Role-to-Endpoint Mapping

### API Service (`/api/*`)

| Endpoint | Method | Required Role |
|---|---|---|
| `/api/chat` | POST | `Api.Chat.Write` |
| `/api/sessions` | GET | `Api.Chat.Read` |
| `/api/sessions` | POST | `Api.Sessions.Manage` |
| `/api/sessions/{id}` | GET | `Api.Chat.Read` |
| `/api/sessions/{id}` | DELETE | `Api.Sessions.Manage` |
| `/api/agents` | GET | `Api.Chat.Read` |
| `/api/agents/call` | POST | `Api.Chat.Write` |
| `/api/agents/workflow` | POST | `Api.Chat.Write` |
| `/api/agents/test` | GET | `Api.Chat.Read` |
| `/api/test-ollama` | GET | `Api.Chat.Read` |
| `/api/debug/mcp` | GET | `Api.Chat.Read` |

### MCP Server (`/mcp/*`)

Per-tool role enforcement via `McpToolRoleMiddleware`:

| MCP Tool Name | Required Role |
|---|---|
| `get_time` | `Mcp.Tools.GetTime` |
| `get_weather` | `Mcp.Tools.GetWeather` |
| `convert_units` | `Mcp.Tools.ConvertUnits` |

### A2A Agents (`/a2a/*`)

| Agent | Route | Required Role |
|---|---|---|
| Coordinator | `/a2a/coordinator/*` | `A2A.Coordinator.Access` |
| Planner | `/a2a/planner/*` | `A2A.Planner.Access` |
| Reviewer | `/a2a/reviewer/*` | `A2A.Reviewer.Access` |
| Research | `/a2a/research/*` | `A2A.Research.Access` |
| Code | `/a2a/code/*` | `A2A.Code.Access` |

### Gateway Routes

The Aspire YARP gateway (configured in `AppHost.cs` via `AddYarp`) routes traffic to services. Each backend service validates JWT tokens independently.

| Route | Target | Authentication |
|---|---|---|
| `/api/*` | API Service | Required (JWT Bearer) |
| `/mcp/*` | MCP Server | Required (JWT Bearer) |
| `/a2a/coordinator/*` | Coordinator Agent | Required (JWT Bearer) |
| `/a2a/planner/*` | Planner Agent | Required (JWT Bearer) |
| `/a2a/reviewer/*` | Reviewer Agent | Required (JWT Bearer) |
| `/a2a/research/*` | Research Agent | Required (JWT Bearer) |
| `/a2a/code/*` | Code Agent | Required (JWT Bearer) |
| `/scalar/*` | Scalar Docs | Anonymous |
| `/*` | Web Frontend | OIDC (redirects to Azure AD login) |

---

## 9. How It Works in Code

### Authentication Flow

```
1. User visits Web app → redirected to Azure AD login
2. Azure AD authenticates user via Authorization Code Flow with PKCE
3. Web app receives authorization code, exchanges for tokens
4. Token contains:
   - scp: "access_as_user" (delegated scope — proves user consented)
   - roles: ["Api.Chat.Read", "Api.Chat.Write", ...] (App Roles — what user can do)
5. Web app calls API Service via IDownstreamApi (acquires OBO token from cookie session)
6. Request goes through Aspire YARP gateway → API Service
7. API Service validates token and checks roles claim
8. API Service propagates token via OBO to MCP/A2A agents
9. Each service validates token and checks roles claim independently
```

### JWT Configuration

Maps Azure AD `roles` claim to `ClaimTypes.Role`:

```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    RoleClaimType = "roles"
};
```

### OIDC Configuration (Web Frontend)

```csharp
builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
    options.ResponseType = "code";   // Authorization Code Flow
    options.UsePkce = true;          // with PKCE
});
```

### FastEndpoints

```csharp
public override void Configure()
{
    Post("/chat");
    Roles(AuthRoles.ApiChatWrite);  // = "Api.Chat.Write"
}
```

### Minimal APIs

```csharp
var auth = new AuthorizeAttribute { Roles = AuthRoles.A2APlannerAccess };
app.MapPost("/a2a/message:send", handler).RequireAuthorization(auth);
```

### MCP Tool Middleware

```csharp
if (!context.User.IsInRole(requiredRole))
    return 403;
```

### Manual Check

```csharp
if (httpContext.User.IsInRole("Api.Chat.Write"))
{
    // user has write access
}
```

---

## 10. Token Anatomy

A decoded JWT token for an authenticated user:

```json
{
  "aud": "api://aspireollama",
  "iss": "https://login.microsoftonline.com/<tenant-id>/v2.0",
  "oid": "user-object-id",
  "name": "John Doe",
  "preferred_username": "john@contoso.com",
  "scp": "access_as_user",
  "roles": [
    "Api.Chat.Read",
    "Api.Chat.Write",
    "Api.Sessions.Manage",
    "Mcp.Access",
    "Mcp.Tools.GetTime",
    "Mcp.Tools.GetWeather",
    "Mcp.Tools.ConvertUnits",
    "A2A.Planner.Access"
  ]
}
```

- `scp` — delegated scope granted via consent (enables OBO token acquisition)
- `roles` — App Roles assigned to the user (controls what they can do)

A user without `A2A.Code.ExecuteCsharp` in their `roles` array will receive `403 Forbidden` when attempting to execute C# code.

---

## 11. Scope vs Role Summary

| Concept | Claim | Purpose | Where Defined | How Assigned |
|---|---|---|---|---|
| **Delegated Scope** | `scp` | Consent — proves the user authorized the Web app to act on their behalf | Expose an API → Add a scope | API permissions on AspireOllama-Web |
| **App Role** | `roles` | Authorization — controls what the user can do | App roles → Create app role | Enterprise Applications → Users and groups |

The scope (`access_as_user`) answers: *"Is this app allowed to call the API on behalf of this user?"*

The roles (`Api.Chat.Write`, etc.) answer: *"What is this user allowed to do?"*

---

## 12. Configuration Files Reference

All files involved in the Azure AD setup, grouped by purpose.

### Terraform (Provisions App Registrations)

| File | Purpose |
|---|---|
| `infra/terraform/main.tf` | Creates both app registrations, 32 app roles, delegated scope, service principals, and client secrets |
| `infra/terraform/variables.tf` | Input variables: `tenant_id`, redirect URIs, secret expiry |
| `infra/terraform/outputs.tf` | Outputs client IDs, secrets, audience URI, and appsettings templates |
| `infra/terraform/terraform.tfvars.example` | Template — copy to `terraform.tfvars` and set your tenant ID |

### Authentication Code (`AspireOllama.ServiceDefaults/Authentication/`)

| File | Purpose |
|---|---|
| `AzureAdOptions.cs` | Configuration POCO: `TenantId`, `ClientId`, `ClientSecret`, `Audience`, `Authority` |
| `BackendAuthExtensions.cs` | Adds JWT Bearer authentication for backend services with `roles` claim mapping |
| `FrontendAuthExtensions.cs` | Adds OIDC Authorization Code Flow with PKCE + OBO token acquisition for Web |
| `ServiceTokenExtensions.cs` | Service-to-service token propagation (OBO and Client Credentials) |
| `AuthScopes.cs` | Centralized constants for all 32 RBAC roles and tool/agent role mappings |
| `McpToolScopeMiddleware.cs` | Per-tool RBAC enforcement for MCP tools based on user roles (class name: `McpToolRoleMiddleware`) |

### Gateway

The YARP gateway is a separate project (`AspireOllama.Gateway/`) with routes configured in `appsettings.json`. It uses YARP with Aspire service discovery and LettuceEncrypt for TLS.

### Application Settings

Terraform generates `appsettings.Secrets.json` for each service with `AzureAd` config (see [Section 7](#7-application-configuration)):

| File | App Registration | Notes |
|---|---|---|
| `AspireOllama.Web/appsettings.Secrets.json` | `AspireOllama-Web` | Blazor frontend — OIDC + DownstreamApis |
| `AspireOllama.ApiService/appsettings.Secrets.json` | `AspireOllama` | API service — JWT Bearer validation |
| `AspireOllama.McpServer/appsettings.Secrets.json` | `AspireOllama` | MCP server — JWT Bearer validation |
| `A2A/*/appsettings.Secrets.json` | `AspireOllama` | A2A agents — JWT Bearer validation |

### Service Programs (Wire Up Authentication)

| File | What It Configures |
|---|---|
| `AspireOllama.Web/Program.cs` | Calls `AddFrontendAuthentication()` for OIDC + IDownstreamApi |
| `AspireOllama.ApiService/Program.cs` | Calls `AddBackendAuthentication()` for JWT Bearer |
| `AspireOllama.McpServer/Program.cs` | Calls `AddBackendAuthentication()` + MCP tool scope middleware |
| `A2A/AspireOllama.A2A.CoordinatorAgent/Program.cs` | Calls `AddBackendAuthentication()` for JWT Bearer |
| `A2A/AspireOllama.A2A.PlannerAgent/Program.cs` | Calls `AddBackendAuthentication()` for JWT Bearer |
| `A2A/AspireOllama.A2A.ReviewerAgent/Program.cs` | Calls `AddBackendAuthentication()` for JWT Bearer |
| `A2A/AspireOllama.A2A.ResearchAgent/Program.cs` | Calls `AddBackendAuthentication()` for JWT Bearer |
| `A2A/AspireOllama.A2A.CodeAgent/Program.cs` | Calls `AddBackendAuthentication()` for JWT Bearer |

### Documentation

| File | Purpose |
|---|---|
| `AZURE-AD-SETUP.md` | This file — full Azure AD setup guide |
| `SECURITY.md` | Security architecture: auth flows, token validation, zero trust model |
