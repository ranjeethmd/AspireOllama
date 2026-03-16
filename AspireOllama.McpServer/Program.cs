var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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

// Enable forwarded headers and gateway enforcement
// All external requests must come through the YARP gateway
app.MapDefaultEndpoints();
app.UseGatewayEnforcement();

// Map MCP transport endpoint
app.MapMcp("/mcp");

app.MapGet("/", () => "MCP Server is running. Connect to /mcp for MCP protocol.");
app.MapOpenApiMCP();
await app.RunAsync();

