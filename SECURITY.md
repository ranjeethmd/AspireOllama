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
│  ┌────────────────────────────────┐  ┌────────────────────────────────────┐ │
│  │     Route: /api/*              │  │     Route: /mcp/*                  │ │
│  │     Auth: Authorization Code   │  │     Auth: Bearer (OBO Token)       │ │
│  │     Audience: api://api-service│  │     Audience: api://mcp-server     │ │
│  └───────────────┬────────────────┘  └───────────────┬────────────────────┘ │
└──────────────────┼───────────────────────────────────┼──────────────────────┘
                   │                                   │
                   ▼                                   ▼
          ┌─────────────────┐                 ┌─────────────────┐
          │   API Service   │────OBO Flow────▶│   MCP Server    │
          │  (Chat, Sessions)│                │  (Tools)        │
          └─────────────────┘                 └─────────────────┘
                   │
                   │ Token Exchange
                   ▼
          ┌─────────────────┐
          │    Identity     │
          │    Provider     │
          │  (Entra ID)     │
          └─────────────────┘
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

## Token Scopes and Audiences

| Service | Audience | Scopes |
|---------|----------|--------|
| API Service | `api://aspireollama-api` | `Chat.Read`, `Chat.Write`, `Sessions.Manage` |
| MCP Server | `api://aspireollama-mcp` | `Tools.Execute`, `Tools.Read` |

### OBO Token Claims

When API Service exchanges a token for MCP Server access, the resulting token contains:

```json
{
  "aud": "api://aspireollama-mcp",
  "iss": "https://login.microsoftonline.com/{tenant}/v2.0",
  "sub": "{original-user-object-id}",
  "oid": "{original-user-object-id}",
  "name": "User Name",
  "scp": "Tools.Execute Tools.Read",
  "azp": "api://aspireollama-api"
}
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

### App Registrations Required

| App Registration | Purpose | Type |
|-----------------|---------|------|
| AspireOllama Gateway | YARP Gateway | Web App (Confidential) |
| AspireOllama API | API Service | Web API |
| AspireOllama MCP | MCP Server | Web API |

### 1. Gateway App Registration

```
Application (client) ID: {gateway-client-id}
Redirect URIs: https://localhost:{port}/signin-oidc
API Permissions:
  - AspireOllama API: Chat.Read, Chat.Write, Sessions.Manage
  - AspireOllama MCP: Tools.Execute, Tools.Read (for OBO)
```

### 2. API Service App Registration

```
Application (client) ID: {api-client-id}
Expose an API:
  - Application ID URI: api://aspireollama-api
  - Scopes: Chat.Read, Chat.Write, Sessions.Manage
API Permissions:
  - AspireOllama MCP: Tools.Execute, Tools.Read (for OBO)
```

### 3. MCP Server App Registration

```
Application (client) ID: {mcp-client-id}
Expose an API:
  - Application ID URI: api://aspireollama-mcp
  - Scopes: Tools.Execute, Tools.Read
Authorized client applications:
  - {api-client-id} (allows OBO from API Service)
```

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

- YARP caches tokens in `IDistributedCache` (Redis recommended for production)
- Token refresh handled automatically before expiration
- OBO tokens cached per user + scope combination

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

- [ ] Register applications in Entra ID
- [ ] Configure app secrets in Key Vault
- [ ] Set up redirect URIs for each environment
- [ ] Configure CORS policies
- [ ] Enable HTTPS everywhere
- [ ] Configure token caching (Redis)
- [ ] Set up audit logging
- [ ] Configure rate limiting
- [ ] Test OBO flow end-to-end
- [ ] Verify tool scope restrictions
