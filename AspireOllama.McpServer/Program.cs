using AspireOllama.ServiceDefaults.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddBackendAuthentication();

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "AspireOllama MCP Tools Server",
            Version = "1.0.0"
        };
    })
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseBackendAuthentication();

// Per-tool role middleware: checks fine-grained roles on tools/call requests
app.UseMiddleware<McpToolRoleMiddleware>();

app.MapMcp("/mcp");

app.MapGet("/", () => "MCP Server is running. Connect to /mcp for MCP protocol.");
app.MapOpenApiMCP();
await app.RunAsync();
