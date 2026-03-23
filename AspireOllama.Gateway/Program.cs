using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Ensure W3C trace context propagation (traceparent/tracestate headers)
DistributedContextPropagator.Current = DistributedContextPropagator.CreateDefaultPropagator();

// Aspire service defaults: service discovery, OpenTelemetry (traces, metrics, logs), health checks
builder.AddServiceDefaults();

// Add YARP and gateway activity sources for distributed tracing
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("Yarp.ReverseProxy")
        .AddSource("AspireOllama.Gateway"));

// Allow large file uploads through the gateway (100MB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024;
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

// Let's Encrypt automatic TLS via ACME protocol
// Disabled when USE_CLOUDFLARE=true (Cloudflare Tunnel handles TLS at the edge)
// Also skipped when no domain is configured (Aspire dev)
var useCloudflare = builder.Configuration.GetValue<bool>("USE_CLOUDFLARE");
var letsEncryptDomain = builder.Configuration["LettuceEncrypt:DomainNames:0"];
if (!useCloudflare && !string.IsNullOrWhiteSpace(letsEncryptDomain))
{
    builder.Services.AddLettuceEncrypt();
}

var app = builder.Build();

app.MapDefaultEndpoints();

// Redirect HTTP to HTTPS — disabled when Cloudflare handles TLS or in development
var useCloudflareForTls = app.Configuration.GetValue<bool>("USE_CLOUDFLARE");
if (!app.Environment.IsDevelopment() && !useCloudflareForTls)
{
    app.UseHttpsRedirection();
}

var gatewaySource = new ActivitySource("AspireOllama.Gateway");

app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use(async (context, next) =>
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AspireOllama.Gateway.Proxy");

        // Internal span so the funnel sees one Server per service, not two
        using var activity = gatewaySource.StartActivity("gateway.proxy", ActivityKind.Internal);

        var traceId = activity?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
        context.Response.Headers["X-Request-Id"] = traceId;

        var sw = Stopwatch.StartNew();
        var route = context.GetReverseProxyFeature()?.Route?.Config?.RouteId ?? "unknown";
        var cluster = context.GetReverseProxyFeature()?.Route?.Config?.ClusterId ?? "unknown";

        activity?.SetTag("gateway.route", route);
        activity?.SetTag("gateway.cluster", cluster);
        activity?.SetTag("http.method", context.Request.Method);
        activity?.SetTag("http.target", context.Request.Path.ToString());

        logger.LogInformation("Gateway [{Route}] -> {Cluster}: {Method} {Path} TraceId={TraceId}",
            route, cluster, context.Request.Method, context.Request.Path, traceId);

        await next();

        sw.Stop();
        var status = context.Response.StatusCode;

        activity?.SetTag("http.status_code", status);
        activity?.SetTag("gateway.duration_ms", sw.ElapsedMilliseconds);

        logger.LogInformation("Gateway [{Route}] <- {Cluster}: {Status} in {ElapsedMs}ms TraceId={TraceId}",
            route, cluster, status, sw.ElapsedMilliseconds, traceId);
    });
});

app.Run();
