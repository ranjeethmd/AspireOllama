using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OllamaSharp;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
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
        // Load Terraform-generated secrets (AzureAd, Gateway config)
        builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: false);

        // Suppress noisy heartbeat/health check logging
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Routing.EndpointMiddleware", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Server.Kestrel.Transport", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Warning);

        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        // Configure forwarded headers for YARP gateway support
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                                       ForwardedHeaders.XForwardedProto |
                                       ForwardedHeaders.XForwardedHost;
            // Trust all proxies in the Aspire/Docker network
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default with extended timeouts for AI models
            http.AddStandardResilienceHandler(options =>
            {
                // Extended timeouts for multi-agent workflows with large models
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(15);
                options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(15);

                // Minimal retries — AI requests are expensive
                options.Retry.MaxRetryAttempts = 1;

                // Circuit breaker: sampling duration must be >= 2x attempt timeout
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(32);
            });

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var otelServiceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? builder.Environment.ApplicationName;
        var otelNamespace = builder.Configuration["OTEL_SERVICE_NAMESPACE"] ?? "AspireOllama";
        var otelVersion = builder.Configuration["OTEL_SERVICE_VERSION"] ?? "1.0.0";
        var otelEnvironment = builder.Configuration["OTEL_DEPLOYMENT_ENVIRONMENT"] ?? builder.Environment.EnvironmentName;

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(
                    serviceName: otelServiceName,
                    serviceNamespace: otelNamespace,
                    serviceVersion: otelVersion,
                    serviceInstanceId: Environment.MachineName);
                resource.AddAttributes([
                    new("deployment.environment", otelEnvironment),
                    new("service.group", otelNamespace),
                ]);
            })
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

        // Health check endpoints — required for Docker/K8s healthchecks in all environments
        app.MapHealthChecks(HealthEndpointPath);
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

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
    /// Includes OBO token propagation and gateway secret for zero trust.
    /// </summary>
    public static void AddMcpServerClient(this WebApplicationBuilder builder)
    {
        string connectionString = SafeConnectionString(builder, "mcpserver");

        builder.Services.AddHttpClient("mcpserver", client =>
        {
            client.BaseAddress = new Uri(connectionString);
        })
        .AddOboTokenPropagation();
    }

    /// <summary>
    /// Adds an HttpClient for an A2A agent using Aspire connection string.
    /// Includes OBO token propagation and gateway secret for zero trust.
    /// </summary>
    public static void AddA2AClient(this WebApplicationBuilder builder, string clientName, string connectionName)
    {
        string connectionString = SafeConnectionString(builder, connectionName);

        builder.Services.AddHttpClient(clientName, client =>
        {
            client.BaseAddress = new Uri(connectionString);
        })
        .AddOboTokenPropagation();
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

}

