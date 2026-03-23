using AspireOllama.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Authentication extensions for Web Frontend (Blazor).
/// Uses Authorization Code Flow with PKCE (OIDC) for user authentication.
/// Uses Microsoft.Identity.Web.DownstreamApi for OBO token acquisition on downstream calls.
/// </summary>
public static class FrontendAuthExtensions
{
    /// <summary>
    /// Adds Authorization Code Flow with PKCE authentication for the Web Frontend.
    /// Configures DownstreamApi for OBO token acquisition to call backend services.
    /// </summary>
    public static TBuilder AddFrontendAuthentication<TBuilder>(
        this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var config = builder.Configuration;
        var azureAdOptions = config.GetSection(AzureAdOptions.SectionName).Get<AzureAdOptions>();

        if (azureAdOptions == null || string.IsNullOrEmpty(azureAdOptions.TenantId))
        {
            throw new InvalidOperationException(
                "Azure AD configuration is required. Configure the AzureAd section with TenantId, ClientId, and ClientSecret.");
        }

        builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(config.GetSection(AzureAdOptions.SectionName))
        .EnableTokenAcquisitionToCallDownstreamApi([$"{azureAdOptions.Audience}/access_as_user"])
        .AddDownstreamApi("ApiService", config.GetSection("DownstreamApis:ApiService"))
        .AddDownstreamApi("McpServer", config.GetSection("DownstreamApis:McpServer"))
        .AddDownstreamApi("A2AAgents", config.GetSection("DownstreamApis:A2AAgents"))
        .AddDistributedTokenCaches();

        // Configure cookie options (AddMicrosoftIdentityWebApp already registers the Cookies scheme)
        builder.Services.Configure<CookieAuthenticationOptions>(
            CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            options.Cookie.Name = "AspireOllama.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        });

        // Authorization Code Flow with PKCE
        builder.Services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.ResponseType = "code";
            options.UsePkce = true;

            // Map Azure AD "roles" claim so [Authorize(Roles = "...")] works in Blazor
            options.TokenValidationParameters.RoleClaimType = "roles";
        });

        builder.Services.AddAuthorization();
        builder.Services.AddMicrosoftIdentityConsentHandler();

        return builder;
    }

    /// <summary>
    /// Configures the Web Frontend to use authentication.
    /// </summary>
    public static WebApplication UseFrontendAuthentication(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();

        // Sign-in endpoint: triggers OIDC challenge to re-acquire tokens
        app.MapGet("MicrosoftIdentity/Account/SignIn", async (HttpContext context) =>
        {
            await context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme,
                new AuthenticationProperties { RedirectUri = "/" });
        }).AllowAnonymous();

        // Sign-out endpoint: clears cookies and signs out of Azure AD
        app.MapGet("MicrosoftIdentity/Account/SignOut", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme,
                new AuthenticationProperties { RedirectUri = "/" });
        }).AllowAnonymous();

        return app;
    }
}
