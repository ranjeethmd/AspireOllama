using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Aspire service defaults: service discovery, OpenTelemetry (traces, metrics, logs), health checks
builder.AddServiceDefaults();

// Add YARP-specific activity source for distributed tracing
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource("Yarp.ReverseProxy"));

// Allow large file uploads through the gateway (100MB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024;
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

// Let's Encrypt automatic TLS via ACME protocol
// Only enabled when domain is configured (production) — skipped in Aspire dev
var letsEncryptDomain = builder.Configuration["LettuceEncrypt:DomainNames:0"];
if (!string.IsNullOrWhiteSpace(letsEncryptDomain))
{
    builder.Services.AddLettuceEncrypt();
}

var app = builder.Build();

app.MapDefaultEndpoints();

// Redirect HTTP to HTTPS in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use(async (context, next) =>
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AspireOllama.Gateway.Proxy");

        var sw = Stopwatch.StartNew();
        var route = context.GetReverseProxyFeature()?.Route?.Config?.RouteId ?? "unknown";
        var cluster = context.GetReverseProxyFeature()?.Route?.Config?.ClusterId ?? "unknown";

        logger.LogInformation("Gateway [{Route}] -> {Cluster}: {Method} {Path}",
            route, cluster, context.Request.Method, context.Request.Path);

        await next();

        sw.Stop();
        var status = context.Response.StatusCode;

        logger.LogInformation("Gateway [{Route}] <- {Cluster}: {Status} in {ElapsedMs}ms",
            route, cluster, status, sw.ElapsedMilliseconds);

        // Tag the current activity with proxy metadata for traces
        Activity.Current?.SetTag("gateway.route", route);
        Activity.Current?.SetTag("gateway.cluster", cluster);
        Activity.Current?.SetTag("gateway.upstream.status_code", status);
        Activity.Current?.SetTag("gateway.upstream.duration_ms", sw.ElapsedMilliseconds);
    });
});

app.Run();
