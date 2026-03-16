using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        // Configure forwarded headers for YARP gateway support
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                                       ForwardedHeaders.XForwardedProto |
                                       ForwardedHeaders.XForwardedHost;
            // Trust all proxies in the Aspire network
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default with extended timeouts for AI models
            http.AddStandardResilienceHandler(options =>
            {
                // Extended timeouts for AI processing (models can be slow)
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);
                options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(5);

                // Minimal retries - AI requests are expensive
                options.Retry.MaxRetryAttempts = 1;
                options.Retry.Delay = TimeSpan.FromSeconds(1);

                // Circuit breaker: sampling duration must be >= 2x attempt timeout
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(12);
            });

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Enable forwarded headers for YARP gateway support
        app.UseForwardedHeaders();

        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks(HealthEndpointPath);

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }

    public static void AddOlamaSharpClient(this WebApplicationBuilder builder, string model)
    {
        var connectionString = SafeConnectionString(builder, "ollama");

        builder.Services.AddHttpClient("ollama", client =>
        {
            client.BaseAddress = new Uri(connectionString);
        });

        builder.Services.AddSingleton<Lazy<IOllamaApiClient>>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("ollama");
            return new(() => new OllamaApiClient(httpClient) { SelectedModel = model });
        });
    }

    /// <summary>
    /// Adds an HttpClient for the MCP server using Aspire connection string.
    /// </summary>
    public static void AddMcpServerClient(this WebApplicationBuilder builder)
    {
        string connectionString = SafeConnectionString(builder, "mcpserver");

        builder.Services.AddHttpClient("mcpserver", client =>
        {
            client.BaseAddress = new Uri(connectionString);
        });
    }

    public static void AddA2AClient(this WebApplicationBuilder builder, string clientName, string connectionName)
    {
        string connectionString = SafeConnectionString(builder, connectionName);

        builder.Services.AddHttpClient(clientName, client =>
        {
            client.BaseAddress = new Uri(connectionString);
        });
    }

    private static string SafeConnectionString(WebApplicationBuilder builder, string connectionName)
    {
        var connectionString = builder.Configuration.GetConnectionString(connectionName);

        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = $"http://{connectionName}";

        const string prefix = "Endpoint=";
        if (connectionString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            connectionString = connectionString[prefix.Length..];

        return connectionString;
    }

    /// <summary>
    /// Adds middleware that enforces requests must come through the YARP gateway.
    /// </summary>
    public static WebApplication UseGatewayEnforcement(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? "";

            // Allow health check endpoints without gateway validation
            if (path.StartsWith(HealthEndpointPath) || path.StartsWith(AlivenessEndpointPath))
            {
                await next();
                return;
            }

            // Check if request came through YARP gateway
            // YARP adds X-Forwarded-For header to proxied requests
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            var forwardedHost = context.Request.Headers["X-Forwarded-Host"].FirstOrDefault();

            // Allow requests that have forwarding headers (came through gateway)
            // or internal Aspire service-to-service calls (no external IP)
            if (!string.IsNullOrEmpty(forwardedFor) || !string.IsNullOrEmpty(forwardedHost))
            {
                await next();
                return;
            }

            // Check if it's an internal Aspire call (localhost or container network)
            var remoteIp = context.Connection.RemoteIpAddress?.ToString();
            if (remoteIp == "127.0.0.1" || remoteIp == "::1" || remoteIp?.StartsWith("10.") == true ||
                remoteIp?.StartsWith("172.") == true || remoteIp?.StartsWith("192.168.") == true)
            {
                await next();
                return;
            }

            // Block external requests that didn't come through gateway
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Direct access forbidden",
                message = "All requests must go through the YARP gateway"
            });
        });

        return app;
    }
}
