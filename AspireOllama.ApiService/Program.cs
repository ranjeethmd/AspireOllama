using AspireOllama.ApiService.Data;
using AspireOllama.ApiService.Services.A2A;
using AspireOllama.ApiService.Services.AI;
using AspireOllama.ApiService.Services.Document;
using AspireOllama.ApiService.Services.Mcp;
using AspireOllama.ApiService.Services.Message;
using AspireOllama.ApiService.Services.Session;
using AspireOllama.ApiService.Services.Tools;
using AspireOllama.Shared;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// Aspire Service Defaults
// ============================================================

// Add health checks, OpenTelemetry, and other Aspire integrations
builder.AddServiceDefaults();

// ============================================================
// Authentication & Authorization (Zero Trust)
// ============================================================

builder.AddBackendAuthentication();

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

// Register Ollama chat clients using Microsoft.Extensions.AI abstraction
// Two models: llava for vision, llama3.1 for tool calling
builder.AddOllamaApiClient("llava")
    .AddKeyedChatClient("vision");  // Vision model for image understanding

builder.AddOllamaApiClient("llama")
    .AddKeyedChatClient("tools");   // Tool-calling model for function calling

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
// Tool Calling Configuration
// ============================================================

// Bind tool configuration from appsettings.json
builder.Services.Configure<ToolConfiguration>(
    builder.Configuration.GetSection("Tools"));

// HTTP client factory for web search tool
builder.Services.AddHttpClient();

// Named HttpClient for MCP server - uses Aspire connection string + OBO token propagation
builder.AddMcpServerClient();

// Built-in tools
builder.Services.AddSingleton<CalculatorTool>();
builder.Services.AddSingleton<WebSearchTool>();
builder.Services.AddSingleton<CodeExecutionTool>();
builder.Services.AddSingleton<FileOperationsTool>();
builder.Services.AddScoped<ImageAnalysisTool>(); // Scoped because it uses keyed chat client
builder.Services.AddScoped<DocumentAnalysisTool>(); // Scoped because it uses keyed chat client

// Tool registry (collects all tools) - scoped to support scoped tools
builder.Services.AddScoped<IToolRegistry, ToolRegistry>();

// MCP service (connects to external MCP servers as client)
builder.Services.AddSingleton<IMcpService, McpService>();

// ============================================================
// A2A Agent Configuration
// ============================================================

// HTTP clients for A2A agents - uses Aspire connection strings + OBO token propagation
builder.AddA2AClient("planner", "planner-agent");
builder.AddA2AClient("reviewer", "reviewer-agent");
builder.AddA2AClient("research", "research-agent");
builder.AddA2AClient("code", "code-agent");

// A2A service (coordinates agent-to-agent communication)
builder.Services.AddSingleton<IA2AService, A2AService>();

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
// MCP Client Initialization
// ============================================================

// Initialize MCP service to connect to external MCP servers at startup
// This helps surface connection errors early
using (var scope = app.Services.CreateScope())
{
    var mcpService = scope.ServiceProvider.GetRequiredService<IMcpService>();
    try
    {
        await mcpService.InitializeAsync();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var servers = mcpService.GetConnectedServers();
        logger.LogInformation("MCP initialized with {Count} connected servers: {Servers}",
            servers.Count, string.Join(", ", servers));
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to initialize MCP service");
    }
}

// ============================================================
// HTTP Pipeline Configuration
// ============================================================

app.MapDefaultEndpoints();
app.UseBackendAuthentication();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
}

// OpenAPI spec for Scalar aggregation (default path /openapi/v1.json)
app.MapOpenApi();

// Map FastEndpoints routes with /api prefix for YARP gateway routing
// Gateway routes /api/* to this service
app.UseFastEndpoints(config =>
{
    config.Endpoints.RoutePrefix = "api";
});

app.MapGet("/", () => "API service is running. Access via YARP gateway at /api/*.");
app.MapGet("/api", () => "API service is running. Use /api/chat, /api/sessions, etc.");

await app.RunAsync();
