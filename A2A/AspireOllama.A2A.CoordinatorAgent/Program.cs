using AspireOllama.A2A.CoordinatorAgent;
using AspireOllama.A2A.Shared;
using AspireOllama.ServiceDefaults.Authentication;
using AspireOllama.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddBackendAuthentication();
builder.AddOlamaSharpClient(OllamaModels.ChatModel);
builder.AddA2AServices();
builder.AddA2AServer<CoordinatorA2AServer>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseBackendAuthentication();
app.MapA2AEndpoints<CoordinatorA2AServer>(AuthRoles.A2ACoordinatorAccess);

await app.RunAsync();
