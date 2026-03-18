using AspireOllama.A2A.CodeAgent;
using AspireOllama.A2A.Shared;
using AspireOllama.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Authorization;
using OllamaSharp;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddOlamaSharpClient("llama3.1");
builder.AddBackendAuthentication();

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

// Register HTTP clients for A2A communication (OBO: propagate user token through agent chain)
builder.Services.AddHttpClient("planner", client => client.BaseAddress = new Uri(builder.Configuration["A2A:KnownAgents:planner"] ?? "http://planner-agent"))
    .AddOboTokenPropagation();
builder.Services.AddHttpClient("reviewer", client => client.BaseAddress = new Uri(builder.Configuration["A2A:KnownAgents:reviewer"] ?? "http://reviewer-agent"))
    .AddOboTokenPropagation();
builder.Services.AddHttpClient("research", client => client.BaseAddress = new Uri(builder.Configuration["A2A:KnownAgents:research"] ?? "http://research-agent"))
    .AddOboTokenPropagation();

// Register A2A services
builder.Services.AddSingleton<IA2AAgentClient, A2AAgentClient>();
builder.Services.AddSingleton<CodeA2AServer>();

var app = builder.Build();

var a2aServer = app.Services.GetRequiredService<CodeA2AServer>();

app.MapDefaultEndpoints();
app.UseBackendAuthentication();

// A2A Protocol endpoints - require A2A.Code.Access role
var auth = new AuthorizeAttribute { Roles = AuthRoles.A2ACodeAccess };
app.MapGet("/.well-known/agent.json", () => a2aServer.GetAgentCard());
app.MapPost("/a2a/message:send", async (SendMessageRequest request, CancellationToken ct) =>
    await a2aServer.HandleSendMessageAsync(request, ct))
    .RequireAuthorization(auth);
app.MapGet("/a2a/tasks/{taskId}", (string taskId) =>
    a2aServer.GetTask(taskId) is { } task ? Results.Ok(task) : Results.NotFound())
    .RequireAuthorization(auth);
app.MapGet("/a2a/tasks", () => a2aServer.GetAllTasks())
    .RequireAuthorization(auth);
app.MapPost("/a2a/tasks/{taskId}:cancel", (string taskId) =>
    a2aServer.CancelTask(taskId) ? Results.Ok() : Results.NotFound())
    .RequireAuthorization(auth);

app.MapGet("/", () => "Code Agent v2.0 (A2A) is running. Endpoint: /.well-known/agent.json");

await app.RunAsync();
