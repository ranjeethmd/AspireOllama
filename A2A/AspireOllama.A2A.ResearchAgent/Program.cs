using AspireOllama.A2A.ResearchAgent;
using AspireOllama.A2A.Shared;
using AspireOllama.ServiceDefaults.Authentication;
using AspireOllama.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddBackendAuthentication();
builder.AddOlamaSharpClient(OllamaModels.ChatModel);
builder.AddA2AServices();
builder.AddA2AServer<ResearchA2AServer>();

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseBackendAuthentication();
app.MapA2AEndpoints<ResearchA2AServer>(AuthRoles.A2AResearchAccess);

await app.RunAsync();
