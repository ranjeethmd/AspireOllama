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
var mcpServer = builder.AddProject<Projects.AspireOllama_McpServer>("mcpserver")
    .WithUrl("/mcp", "MCP Endpoint")
    .WithHttpHealthCheck("/health");

// ============================================================
// A2A Agents (Agent-to-Agent Protocol Servers) - AI-Powered
// Each agent uses OllamaSharp + A2A Protocol for inter-agent communication
// ============================================================
var plannerAgent = builder.AddProject<Projects.AspireOllama_A2A_PlannerAgent>("planner-agent")
    .WithHttpHealthCheck("/health")
    .WithUrl("/.well-known/agent.json", "A2A Agent Card")
    .WithReference(ollama)
    .WaitFor(llama);

var reviewerAgent = builder.AddProject<Projects.AspireOllama_A2A_ReviewerAgent>("reviewer-agent")
    .WithHttpHealthCheck("/health")
    .WithUrl("/.well-known/agent.json", "A2A Agent Card")
    .WithReference(ollama)
    .WaitFor(llama);

var researchAgent = builder.AddProject<Projects.AspireOllama_A2A_ResearchAgent>("research-agent")
    .WithHttpHealthCheck("/health")
    .WithUrl("/.well-known/agent.json", "A2A Agent Card")
    .WithReference(ollama)
    .WaitFor(llama);

var codeAgent = builder.AddProject<Projects.AspireOllama_A2A_CodeAgent>("code-agent")
    .WithHttpHealthCheck("/health")
    .WithUrl("/.well-known/agent.json", "A2A Agent Card")
    .WithReference(ollama)
    .WaitFor(llama);

// Configure A2A agent discovery - each agent knows about the others
plannerAgent
    .WithEnvironment("A2A__KnownAgents__reviewer", reviewerAgent.GetEndpoint("http"))
    .WithEnvironment("A2A__KnownAgents__research", researchAgent.GetEndpoint("http"))
    .WithEnvironment("A2A__KnownAgents__code", codeAgent.GetEndpoint("http"));

reviewerAgent
    .WithEnvironment("A2A__KnownAgents__planner", plannerAgent.GetEndpoint("http"))
    .WithEnvironment("A2A__KnownAgents__research", researchAgent.GetEndpoint("http"))
    .WithEnvironment("A2A__KnownAgents__code", codeAgent.GetEndpoint("http"));

researchAgent
    .WithEnvironment("A2A__KnownAgents__planner", plannerAgent.GetEndpoint("http"))
    .WithEnvironment("A2A__KnownAgents__reviewer", reviewerAgent.GetEndpoint("http"))
    .WithEnvironment("A2A__KnownAgents__code", codeAgent.GetEndpoint("http"));

codeAgent
    .WithEnvironment("A2A__KnownAgents__planner", plannerAgent.GetEndpoint("http"))
    .WithEnvironment("A2A__KnownAgents__reviewer", reviewerAgent.GetEndpoint("http"))
    .WithEnvironment("A2A__KnownAgents__research", researchAgent.GetEndpoint("http"));

// ============================================================
// API Service (Chat API + HTTP MCP Server)
// ============================================================
var apiService = builder.AddProject<Projects.AspireOllama_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

apiService
    .WithUrl("/scalar/v1", "API Docs");

apiService
    .WithReference(llava)
    .WithReference(llama)
    .WithReference(mcpServer)
    .WithReference(plannerAgent)
    .WithReference(reviewerAgent)
    .WithReference(researchAgent)
    .WithReference(codeAgent)
    .WaitFor(llava)
    .WaitFor(llama)
    .WaitFor(mcpServer)
    .WaitFor(plannerAgent)
    .WaitFor(reviewerAgent)
    .WaitFor(researchAgent)
    .WaitFor(codeAgent);

// ============================================================
// Web Frontend (Blazor Chat UI)
// ============================================================
builder.AddProject<Projects.AspireOllama_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
