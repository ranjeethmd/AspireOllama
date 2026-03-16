using Scalar.Aspire;

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
// Internal only - accessed through Gateway
// ============================================================
var mcpServer = builder.AddProject<Projects.AspireOllama_McpServer>("mcpserver")
    .WithHttpHealthCheck("/health");

// ============================================================
// A2A Agents (Agent-to-Agent Protocol Servers) - AI-Powered
// Each agent uses OllamaSharp + A2A Protocol for inter-agent communication
// Internal only - accessed through Gateway
// Agents connect to Ollama via gateway route /ollama/*
// ============================================================
var plannerAgent = builder.AddProject<Projects.AspireOllama_A2A_PlannerAgent>("planner-agent")
    .WithHttpHealthCheck("/health");

var reviewerAgent = builder.AddProject<Projects.AspireOllama_A2A_ReviewerAgent>("reviewer-agent")
    .WithHttpHealthCheck("/health");

var researchAgent = builder.AddProject<Projects.AspireOllama_A2A_ResearchAgent>("research-agent")
    .WithHttpHealthCheck("/health");

var codeAgent = builder.AddProject<Projects.AspireOllama_A2A_CodeAgent>("code-agent")
    .WithHttpHealthCheck("/health");

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
// API Service (Chat API)
// Internal only - accessed through Gateway
// ============================================================
var apiService = builder.AddProject<Projects.AspireOllama_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

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
// Internal only - accessed through Gateway
// ============================================================
var webFrontend = builder.AddProject<Projects.AspireOllama_Web>("webfrontend")
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

// ============================================================
// Consolidated Scalar API Documentation
// Aggregates OpenAPI specs from all services
// Accessible via gateway at /scalar
// ============================================================
var scalar = builder.AddScalarApiReference();


// Register all services with Scalar for consolidated documentation
scalar
    .WithApiReference(apiService, configureOptions: options =>
    {
        options.AddDocument("v1", "REST API - Chat, Sessions, Agents");
    })


    .WithApiReference(mcpServer, configureOptions: options =>
    {
        options.AddDocument("v1", "MCP Server - Model Context Protocol Tools");
    })    
    .WithApiReference(plannerAgent, configureOptions: options =>
    {
        options.AddDocument("v1", "Planner Agent - A2A Protocol");
    })
    .WithApiReference(reviewerAgent, configureOptions: options =>
    {
        options.AddDocument("v1", "Reviewer Agent - A2A Protocol");
    })
    .WithApiReference(researchAgent, configureOptions: options =>
    {
        options.AddDocument("v1", "Research Agent - A2A Protocol");
    })
    .WithApiReference(codeAgent, configureOptions: options =>
    {
        options.AddDocument("v1", "Code Agent - A2A Protocol");
    });

// ============================================================
// YARP Gateway - Single entry point for all external traffic
// Routes:
//   /api/*         -> API Service (REST API)
//   /mcp/*         -> MCP Server (Model Context Protocol)
//   /ollama/*      -> Ollama LLM Service (internal access for agents)
//   /a2a/planner/* -> Planner Agent (A2A Protocol)
//   /a2a/reviewer/* -> Reviewer Agent (A2A Protocol)
//   /a2a/research/* -> Research Agent (A2A Protocol)
//   /a2a/code/*    -> Code Agent (A2A Protocol)
//   /scalar        -> Consolidated API Documentation
//   /*             -> Web Frontend (Blazor UI)
// ============================================================
// Configure agents to connect to Ollama directly
plannerAgent.WithReference(ollama).WaitFor(llama);
reviewerAgent.WithReference(ollama).WaitFor(llama);
researchAgent.WithReference(ollama).WaitFor(llama);
codeAgent.WithReference(ollama).WaitFor(llama);

var gateway = builder.AddYarp("gateway")
    .WithReference(apiService)
    .WithReference(mcpServer)
    .WithReference(plannerAgent)
    .WithReference(reviewerAgent)
    .WithReference(researchAgent)
    .WithReference(codeAgent)
    .WithReference(webFrontend)
    .WithReference(scalar)
    .WithConfiguration(yarp =>
    {
        // API Service routes
        yarp.AddRoute("/api/{**catch-all}", apiService);

        // MCP Server routes
        yarp.AddRoute("/mcp/{**catch-all}", mcpServer);

        // A2A Agent routes
        yarp.AddRoute("/a2a/planner/{**catch-all}", plannerAgent);
        yarp.AddRoute("/a2a/reviewer/{**catch-all}", reviewerAgent);
        yarp.AddRoute("/a2a/research/{**catch-all}", researchAgent);
        yarp.AddRoute("/a2a/code/{**catch-all}", codeAgent);

        // Scalar API Documentation
        yarp.AddRoute("/scalar/{**catch-all}", scalar);

        // Web Frontend (catch-all, must be last)
        yarp.AddRoute(webFrontend);
    });

builder.Build().Run();
