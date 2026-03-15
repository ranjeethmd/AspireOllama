var builder = DistributedApplication.CreateBuilder(args);

// ============================================================
// Ollama LLM Service
// ============================================================
var ollama = builder.AddOllama("ollama")
    .WithGPUSupport()
    .WithDataVolume()
    .WithOpenWebUI();
var llava = ollama.AddModel("llava", "llava");       // Vision model (image understanding)
var llama = ollama.AddModel("llama", "llama3.1");    // Tool-calling model (function calling)

// ============================================================
// MCP Server (HTTP-based tools server)
// ============================================================
// The MCP server exposes tools via HTTP MCP protocol
// ApiService connects to it via Aspire service discovery
var mcpServer = builder.AddProject<Projects.AspireOllama_McpServer>("mcpserver")
    .WithUrl("/mcp", "MCP Endpoint")
    .WithHttpHealthCheck("/health");

// ============================================================
// API Service (Chat API + HTTP MCP Server)
// ============================================================
var apiService = builder.AddProject<Projects.AspireOllama_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

// Dashboard URLs
apiService
    .WithUrl("/scalar/v1", "API Docs");
   

// Dependencies
apiService
    .WithReference(llava)
    .WithReference(llama)
    .WithReference(mcpServer)
    .WaitFor(llava)
    .WaitFor(llama)
    .WaitFor(mcpServer);

// ============================================================
// Web Frontend (Blazor Chat UI)
// ============================================================
builder.AddProject<Projects.AspireOllama_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
