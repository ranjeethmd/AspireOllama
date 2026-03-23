using AspireOllama.Web;
using AspireOllama.Web.Components;
using Microsoft.Identity.Abstractions;

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
    // Allow large file uploads for RAG documents (base64 inflates ~33%)
    options.MaximumReceiveMessageSize = 150 * 1024 * 1024; // 150MB
});

builder.Services.AddOutputCache();

// ============================================================
// API Client Configuration (IDownstreamApi handles OBO token acquisition)
// Gateway secret is injected by AddServiceDefaults() via ConfigureHttpClientDefaults
// ============================================================

// Extend HTTP client timeout for large file uploads to downstream APIs
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(15));
});

// Allow large file uploads via multipart/form-data
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100MB
});

builder.Services.AddScoped<ChatApiClient>();
builder.Services.AddScoped<UserRoleService>();

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

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:;";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    await next();
});

app.UseAntiforgery();   // CSRF protection for Blazor forms
app.UseOutputCache();

// Serve static files (CSS, JS, images)
app.MapStaticAssets();

// File upload endpoint — handles multipart/form-data directly (bypasses SignalR)
// Uses IDownstreamApi with HttpContext.User (not AuthenticationStateProvider which is Blazor-only)
app.MapPost("/upload-documents", async (HttpRequest request, IDownstreamApi downstreamApi) =>
{
    if (!request.HasFormContentType)
        return Results.BadRequest("Expected multipart/form-data");

    var form = await request.ReadFormAsync();
    var files = new List<AspireOllama.Shared.FileAttachment>();

    foreach (var formFile in form.Files)
    {
        using var ms = new MemoryStream();
        await formFile.CopyToAsync(ms);

        files.Add(new AspireOllama.Shared.FileAttachment
        {
            FileName = formFile.FileName,
            ContentType = formFile.ContentType,
            Base64Data = Convert.ToBase64String(ms.ToArray()),
            Type = AspireOllama.Shared.FileType.Unknown
        });
    }

    if (files.Count == 0)
        return Results.BadRequest("No files uploaded");

    var uploadRequest = new AspireOllama.Shared.DocumentUploadRequest { Files = files };
    var user = request.HttpContext.User;

    var response = await downstreamApi.PostForUserAsync<AspireOllama.Shared.DocumentUploadRequest, AspireOllama.Shared.DocumentUploadResponse>(
        "ApiService", uploadRequest,
        options => options.RelativePath = "api/documents/upload",
        user);

    return Results.Ok(response ?? new AspireOllama.Shared.DocumentUploadResponse());
}).RequireAuthorization();

// Map Blazor components with interactive server rendering
// RequireAuthorization ensures unauthenticated users are redirected to Azure AD login
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization();

await app.RunAsync();
