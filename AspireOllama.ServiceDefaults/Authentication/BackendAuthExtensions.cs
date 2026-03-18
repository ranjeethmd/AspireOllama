using AspireOllama.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Authentication extensions for backend services (API Service, MCP Server, A2A Agents).
/// Zero trust: always validates JWT Bearer tokens. Authorization via Azure AD App Roles.
/// </summary>
public static class BackendAuthExtensions
{
    /// <summary>
    /// Adds JWT Bearer authentication for backend services.
    /// Azure AD App Roles map to ClaimTypes.Role via RoleClaimType = "roles".
    /// Use Roles("Api.Chat.Write") in FastEndpoints or [Authorize(Roles = "...")] on controllers.
    /// </summary>
    public static TBuilder AddBackendAuthentication<TBuilder>(
        this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var config = builder.Configuration;
        var azureAdOptions = config.GetSection(AzureAdOptions.SectionName).Get<AzureAdOptions>();

        if (azureAdOptions == null || string.IsNullOrEmpty(azureAdOptions.TenantId))
        {
            throw new InvalidOperationException(
                "Azure AD configuration is required. Configure the AzureAd section with TenantId, ClientId, and Audience.");
        }

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = azureAdOptions.Authority;

                // Disable default claim type mapping so "roles" stays as "roles"
                // Without this, Microsoft's JWT handler renames claims to long URI types
                options.MapInboundClaims = false;

                // Accept both "api://<client-id>" and bare "<client-id>" as valid audiences
                var clientId = azureAdOptions.ClientId;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidAudiences = new[] { azureAdOptions.Audience, clientId },
                    RoleClaimType = "roles",
                    NameClaimType = "name"
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<JwtBearerHandler>>();
                        logger.LogWarning("Authentication failed: {Error}", context.Exception.Message);
                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();
        });

        return builder;
    }

    /// <summary>
    /// Configures the application to use authentication and authorization.
    /// </summary>
    public static WebApplication UseBackendAuthentication(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    /// <summary>
    /// Gets the current user identity from the HTTP context.
    /// </summary>
    public static UserContext? GetUserContext(this HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return null;

        return new UserContext
        {
            UserId = context.User.FindFirst("oid")?.Value
                ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? "unknown",
            UserName = context.User.FindFirst("name")?.Value
                ?? context.User.FindFirst(ClaimTypes.Name)?.Value
                ?? "Unknown",
            Email = context.User.FindFirst("preferred_username")?.Value
                ?? context.User.FindFirst(ClaimTypes.Email)?.Value,
            Roles = context.User.FindAll("roles")
                .Concat(context.User.FindAll(ClaimTypes.Role))
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }
}

/// <summary>
/// User context extracted from the authenticated token.
/// </summary>
public class UserContext
{
    public required string UserId { get; init; }
    public required string UserName { get; init; }
    public string? Email { get; init; }
    public string[] Roles { get; init; } = [];

    public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}
