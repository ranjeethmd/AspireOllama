using AspireOllama.Web;
using AspireOllama.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// Aspire Service Defaults
// ============================================================

// Add health checks, OpenTelemetry, and other Aspire integrations
builder.AddServiceDefaults();

// ============================================================
// Redis Distributed Cache (for MSAL token cache)
// ============================================================
builder.AddRedisDistributedCache("redis");

// ============================================================
// Authentication (OIDC + OBO for downstream calls)
// ============================================================
builder.AddFrontendAuthentication();

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
// API Client Configuration (IDownstreamApi handles OBO token acquisition)
// Gateway secret is injected by AddServiceDefaults() via ConfigureHttpClientDefaults
// ============================================================

builder.Services.AddScoped<ChatApiClient>();

var app = builder.Build();

// ============================================================
// HTTP Pipeline Configuration
// ============================================================

// Enable forwarded headers (no gateway enforcement — Web is user-facing)
// Unauthenticated users are redirected to Azure AD login (OIDC + PKCE)
app.MapDefaultEndpoints();
app.UseFrontendAuthentication();

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
// RequireAuthorization ensures unauthenticated users are redirected to Azure AD login
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization();

await app.RunAsync();
