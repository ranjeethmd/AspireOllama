using AspireOllama.Web;
using AspireOllama.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// Aspire Service Defaults
// ============================================================

// Add health checks, OpenTelemetry, and other Aspire integrations
builder.AddServiceDefaults();

// ============================================================
// Blazor Server Configuration
// ============================================================

// Add Razor components with interactive server-side rendering
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure Blazor SignalR hub for long-running AI operations
// Extended timeouts prevent disconnects during AI processing
builder.Services.AddServerSideBlazor(options =>
{
    // Keep disconnected circuits alive for reconnection
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(10);
})
.AddHubOptions(options =>
{
    // Client timeout for long AI responses
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(10);
    // Frequent keepalive to maintain connection
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

builder.Services.AddOutputCache();

// ============================================================
// API Client Configuration
// ============================================================

// Configure HTTP client for API service communication
builder.Services.AddHttpClient<ChatApiClient>(client =>
    {
        // Aspire service discovery resolves "apiservice" to actual endpoint
        client.BaseAddress = new("https+http://apiservice");
        client.Timeout = TimeSpan.FromMinutes(10);
    })
    .AddStandardResilienceHandler(options =>
    {
        // Extended timeouts for AI processing (models can be slow)
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(10);
        options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(5);

        // Minimal retries - AI requests are expensive and idempotent
        options.Retry.MaxRetryAttempts = 1;  // Must be >= 1
        options.Retry.Delay = TimeSpan.FromSeconds(1);

        // Circuit breaker: sampling duration must be >= 2x attempt timeout
        // This prevents cascading failures when AI service is slow
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(12);
    });

var app = builder.Build();

// ============================================================
// HTTP Pipeline Configuration
// ============================================================

// Enable forwarded headers and gateway enforcement
// All external requests must come through the YARP gateway
app.MapDefaultEndpoints();
app.UseGatewayEnforcement();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts(); // HTTPS Strict Transport Security
}

app.UseHttpsRedirection();
app.UseAntiforgery();   // CSRF protection for Blazor forms
app.UseOutputCache();

// Serve static files (CSS, JS, images)
app.MapStaticAssets();

// Map Blazor components with interactive server rendering
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

await app.RunAsync();
