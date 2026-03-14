using AspireOllama.ApiService.Data;
using AspireOllama.ApiService.Services.AI;
using AspireOllama.ApiService.Services.Document;
using AspireOllama.ApiService.Services.Message;
using AspireOllama.ApiService.Services.Session;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// Aspire Service Defaults
// ============================================================

// Add health checks, OpenTelemetry, and other Aspire integrations
builder.AddServiceDefaults();

// ============================================================
// HTTP Client Configuration
// ============================================================

// Increase timeout for all HTTP clients including Ollama (AI models can be slow)
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(10));
});

// ============================================================
// AI/Ollama Configuration
// ============================================================

// Register Ollama chat client using Microsoft.Extensions.AI abstraction
// "llama" refers to the connection string name in AppHost (llava vision model)
builder.AddOllamaApiClient("llama")
    .AddChatClient();

// ============================================================
// Database Configuration (SQLite)
// ============================================================

// SQLite database file stored in application content root
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "chat.db");
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// ============================================================
// Application Services (Single Responsibility)
// ============================================================

// Session management service
builder.Services.AddScoped<ISessionService, SessionService>();

// Chat message service
builder.Services.AddScoped<IChatMessageService, ChatMessageService>();

// AI chat service (context building and model calls)
builder.Services.AddScoped<IAiChatService, AiChatService>();

// Document text extraction service (PDF, Word, Excel, PowerPoint)
builder.Services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();

// ============================================================
// API Configuration
// ============================================================

// FastEndpoints for structured API endpoint handling
builder.Services.AddFastEndpoints();

// Problem details for standardized error responses
builder.Services.AddProblemDetails();

// Increase request body size for file uploads (50MB max)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024;
});

// OpenAPI/Swagger documentation
builder.Services.AddOpenApi();

var app = builder.Build();

// ============================================================
// Database Initialization
// ============================================================

// Ensure database and tables exist, apply migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    db.Database.EnsureCreated();

    // Manual migration: Add FilesJson column for document attachment storage
    // Required for databases created before document support was added
    try
    {
        db.Database.ExecuteSqlRaw(
            "ALTER TABLE ChatMessages ADD COLUMN FilesJson TEXT DEFAULT '[]'");
    }
    catch (Microsoft.Data.Sqlite.SqliteException)
    {
        // Column already exists - this is expected for newer databases
    }
}

// ============================================================
// HTTP Pipeline Configuration
// ============================================================

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi(); // Generate OpenAPI docs at /openapi.json 
    app.MapScalarApiReference(); // Mounts the UI at /scalar/v1
}
else
{
    app.UseExceptionHandler();
}

// Map FastEndpoints routes (/chat, /sessions, etc.)
app.UseFastEndpoints();

// Health check and root endpoint
app.MapGet("/", () => "API service is running. Use /test-ollama to verify Ollama connection.");

// Aspire health check endpoints
app.MapDefaultEndpoints();

app.Run();
