using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AspireOllama.A2A.Shared;

/// <summary>
/// Configuration for A2A per-user rate limiting.
/// </summary>
public class A2ARateLimitOptions
{
    public const string SectionName = "A2A:RateLimit";

    /// <summary>Maximum requests per window per user.</summary>
    public int PermitLimit { get; set; } = 20;

    /// <summary>Window duration in seconds.</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>Maximum queued requests when limit is hit.</summary>
    public int QueueLimit { get; set; } = 5;
}

/// <summary>
/// Extension methods for adding per-user rate limiting to A2A agents.
/// </summary>
public static class A2ARateLimitExtensions
{
    public const string PolicyName = "a2a-per-user";

    /// <summary>
    /// Adds per-user rate limiting for A2A endpoints.
    /// Partitions by user identity (oid claim or IP fallback).
    /// Configure via A2A:RateLimit section in appsettings.
    /// </summary>
    public static WebApplicationBuilder AddA2ARateLimiting(this WebApplicationBuilder builder)
    {
        var options = new A2ARateLimitOptions();
        builder.Configuration.GetSection(A2ARateLimitOptions.SectionName).Bind(options);

        builder.Services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.AddPolicy(PolicyName, context =>
            {
                // Partition by user identity — fallback to IP for unauthenticated requests
                var userId = context.User?.FindFirst("oid")?.Value
                    ?? context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.PermitLimit,
                    Window = TimeSpan.FromSeconds(options.WindowSeconds),
                    QueueLimit = options.QueueLimit,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
            });
        });

        return builder;
    }

    /// <summary>
    /// Applies the rate limiting middleware. Call after UseAuthentication/UseAuthorization.
    /// </summary>
    public static WebApplication UseA2ARateLimiting(this WebApplication app)
    {
        app.UseRateLimiter();
        return app;
    }
}
