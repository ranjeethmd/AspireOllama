using AspireOllama.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System.Net.Http.Headers;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extensions for configuring service-to-service token propagation.
/// Supports OBO (propagate user token downstream) and Client Credentials (service identity).
/// </summary>
public static class ServiceTokenExtensions
{
    /// <summary>
    /// Adds an HTTP message handler that propagates the incoming Bearer token (OBO)
    /// to outgoing HTTP requests made by the named HttpClient.
    /// Use this when a backend service calls another backend service on behalf of the user.
    /// The incoming request must have a Bearer token (JWT auth).
    /// </summary>
    public static IHttpClientBuilder AddOboTokenPropagation(this IHttpClientBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.AddHttpMessageHandler<OboTokenPropagationHandler>();
        builder.Services.AddTransient<OboTokenPropagationHandler>();
        return builder;
    }

    /// <summary>
    /// Adds an HTTP message handler that acquires a Client Credentials token
    /// for outgoing HTTP requests. Use this for service-to-service calls
    /// where no user context is needed (e.g., agent-to-agent).
    /// </summary>
    public static IHttpClientBuilder AddClientCredentialsToken(this IHttpClientBuilder builder)
    {
        builder.AddHttpMessageHandler(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILogger<ClientCredentialsTokenHandler>>();
            var options = config.GetSection(AzureAdOptions.SectionName).Get<AzureAdOptions>()
                ?? throw new InvalidOperationException("AzureAd configuration is required.");

            return new ClientCredentialsTokenHandler(options, logger);
        });
        return builder;
    }
}

/// <summary>
/// Propagates the incoming Authorization Bearer token to outgoing HTTP requests.
/// This implements the OBO pattern at the HTTP level: the backend service
/// forwards the token it received to downstream services.
/// </summary>
public class OboTokenPropagationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<OboTokenPropagationHandler> _logger;

    public OboTokenPropagationHandler(
        IHttpContextAccessor httpContextAccessor,
        ILogger<OboTokenPropagationHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            var authHeader = httpContext.Request.Headers.Authorization.FirstOrDefault();
            if (authHeader is not null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = authHeader["Bearer ".Length..];
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                _logger.LogDebug("Propagated OBO token to downstream request: {Uri}", request.RequestUri);
            }
            else
            {
                _logger.LogWarning("No Bearer token found to propagate for request: {Uri}", request.RequestUri);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// Acquires and attaches a Client Credentials token for service-to-service calls.
/// Used when agents call each other (no user context).
/// </summary>
public class ClientCredentialsTokenHandler : DelegatingHandler
{
    private readonly AzureAdOptions _options;
    private readonly ILogger<ClientCredentialsTokenHandler> _logger;
    private readonly IConfidentialClientApplication _msalClient;

    public ClientCredentialsTokenHandler(AzureAdOptions options, ILogger<ClientCredentialsTokenHandler> logger)
    {
        _options = options;
        _logger = logger;

        _msalClient = ConfidentialClientApplicationBuilder
            .Create(options.ClientId)
            .WithClientSecret(options.ClientSecret)
            .WithAuthority(options.Authority)
            .Build();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var scopes = new[] { $"{_options.Audience}/.default" };
            var result = await _msalClient.AcquireTokenForClient(scopes).ExecuteAsync(cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
            _logger.LogDebug("Attached client credentials token for request: {Uri}", request.RequestUri);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire client credentials token for request: {Uri}", request.RequestUri);
            throw;
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
