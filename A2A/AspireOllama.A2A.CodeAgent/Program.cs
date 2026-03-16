using AspireOllama.A2A.CodeAgent;
using AspireOllama.A2A.CodeAgent.Tools;
using AspireOllama.A2A.Shared;
using ModelContextProtocol.Server;
using OllamaSharp;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var ollamaUrl = builder.Configuration["ConnectionStrings:ollama-code"] ?? "http://localhost:11434";
builder.Services.AddSingleton<IOllamaApiClient>(sp =>
{
    var client = new OllamaApiClient(ollamaUrl);
    client.SelectedModel = "llama3.1";
    return client;
});

// Configure A2A known agents
builder.Services.Configure<KnownAgentsOptions>(options =>
{
    options.Agents = new Dictionary<string, string>
    {
        ["planner"] = builder.Configuration["A2A:KnownAgents:planner"] ?? "http://planner-agent",
        ["reviewer"] = builder.Configuration["A2A:KnownAgents:reviewer"] ?? "http://reviewer-agent",
        ["research"] = builder.Configuration["A2A:KnownAgents:research"] ?? "http://research-agent"
    };
});

// Register HTTP clients for A2A communication
builder.Services.AddHttpClient("planner", client => client.BaseAddress = new Uri(builder.Configuration["A2A:KnownAgents:planner"] ?? "http://planner-agent"));
builder.Services.AddHttpClient("reviewer", client => client.BaseAddress = new Uri(builder.Configuration["A2A:KnownAgents:reviewer"] ?? "http://reviewer-agent"));
builder.Services.AddHttpClient("research", client => client.BaseAddress = new Uri(builder.Configuration["A2A:KnownAgents:research"] ?? "http://research-agent"));

// Register A2A services
builder.Services.AddSingleton<IA2AAgentClient, A2AAgentClient>();
builder.Services.AddSingleton<CodeA2AServer>();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "Code Agent",
            Version = "2.0.0"
        };
    })
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

var ollamaClient = app.Services.GetRequiredService<IOllamaApiClient>();
CodeTools.Initialize(ollamaClient);

var a2aServer = app.Services.GetRequiredService<CodeA2AServer>();

app.MapDefaultEndpoints();

// A2A Protocol endpoints
app.MapGet("/.well-known/agent.json", () => a2aServer.GetAgentCard());
app.MapPost("/a2a/message:send", async (SendMessageRequest request, CancellationToken ct) =>
    await a2aServer.HandleSendMessageAsync(request, ct));
app.MapGet("/a2a/tasks/{taskId}", (string taskId) =>
    a2aServer.GetTask(taskId) is { } task ? Results.Ok(task) : Results.NotFound());
app.MapGet("/a2a/tasks", () => a2aServer.GetAllTasks());
app.MapPost("/a2a/tasks/{taskId}:cancel", (string taskId) =>
    a2aServer.CancelTask(taskId) ? Results.Ok() : Results.NotFound());

// MCP endpoints
app.MapMcp("/mcp");
app.MapGet("/", () => "Code Agent v2.0 (AI-Powered + A2A) is running. Endpoints: /mcp (MCP), /.well-known/agent.json (A2A)");
app.MapGet("/health", () => Results.Ok(new { status = "healthy", agent = "code", version = "2.0", ai = true, a2a = true }));

app.Run();
