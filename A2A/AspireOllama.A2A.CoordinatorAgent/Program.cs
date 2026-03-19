using AspireOllama.A2A.CoordinatorAgent;
using AspireOllama.A2A.Shared;
using AspireOllama.ServiceDefaults.Authentication;
using Microsoft.AspNetCore.Authorization;
using OllamaSharp;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddOlamaSharpClient(AspireOllama.Shared.OllamaModels.ChatModel);
builder.AddBackendAuthentication();

// Configure known agents — Coordinator knows about all leaf agents
builder.Services.Configure<KnownAgentsOptions>(options =>
{
    options.Agents = new Dictionary<string, string>
    {
        ["planner"] = builder.Configuration["A2A:KnownAgents:planner"] ?? "http://planner-agent",
        ["reviewer"] = builder.Configuration["A2A:KnownAgents:reviewer"] ?? "http://reviewer-agent",
        ["research"] = builder.Configuration["A2A:KnownAgents:research"] ?? "http://research-agent",
        ["code"] = builder.Configuration["A2A:KnownAgents:code"] ?? "http://code-agent"
    };
});

// HTTP clients for all leaf agents with OBO token propagation
builder.Services.AddHttpClient("planner", client => client.BaseAddress = new Uri(builder.Configuration["A2A:KnownAgents:planner"] ?? "http://planner-agent"))
    .AddOboTokenPropagation();
builder.Services.AddHttpClient("reviewer", client => client.BaseAddress = new Uri(builder.Configuration["A2A:KnownAgents:reviewer"] ?? "http://reviewer-agent"))
    .AddOboTokenPropagation();
builder.Services.AddHttpClient("research", client => client.BaseAddress = new Uri(builder.Configuration["A2A:KnownAgents:research"] ?? "http://research-agent"))
    .AddOboTokenPropagation();
builder.Services.AddHttpClient("code", client => client.BaseAddress = new Uri(builder.Configuration["A2A:KnownAgents:code"] ?? "http://code-agent"))
    .AddOboTokenPropagation();

builder.Services.AddSingleton<IA2AAgentClient, A2AAgentClient>();
builder.Services.AddSingleton<CoordinatorA2AServer>();

var app = builder.Build();

var a2aServer = app.Services.GetRequiredService<CoordinatorA2AServer>();

app.MapDefaultEndpoints();
app.UseBackendAuthentication();

// A2A Protocol endpoints
var auth = new AuthorizeAttribute { Roles = AuthRoles.A2ACoordinatorAccess };
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

app.MapGet("/", () => "Coordinator Agent v1.0 (A2A) is running. Endpoint: /.well-known/agent.json");

await app.RunAsync();
