using AspireOllama.A2A.CodeAgent;
using AspireOllama.A2A.Shared;
using AspireOllama.ServiceDefaults.Authentication;
using AspireOllama.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddBackendAuthentication();
builder.AddOlamaSharpClient(OllamaModels.ChatModel);
builder.AddA2AServices();
builder.AddA2AServer<CodeA2AServer>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseBackendAuthentication();
app.MapA2AEndpoints<CodeA2AServer>(AuthRoles.A2ACodeAccess);

await app.RunAsync();
