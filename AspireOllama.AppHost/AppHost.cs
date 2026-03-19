using Microsoft.Extensions.Configuration;
using Scalar.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

// Load secrets (gitignored)
builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: false);

// ============================================================
// New Relic Observability (via OpenTelemetry OTLP)
// Requires NEW_RELIC_LICENSE_KEY in user secrets or environment
// ============================================================
var newRelicLicenseKey = builder.Configuration["NewRelic:LicenseKey"] ?? "";
var newRelicOtlpEndpoint = builder.Configuration["NewRelic:OtlpEndpoint"] ?? "https://otlp.nr-data.net";
var otelServiceNamespace = builder.Configuration["Otel:ServiceNamespace"] ?? "AspireOllama";
var otelServiceVersion = builder.Configuration["Otel:ServiceVersion"] ?? "1.0.0";
var otelDeploymentEnv = builder.Configuration["Otel:DeploymentEnvironment"] ?? "development";

// ============================================================
// Redis (Distributed Token Cache)
// ============================================================
var redis = builder.AddRedis("redis")
    .WithDataVolume();

// ============================================================
// MongoDB (Chat persistence)
// ============================================================
var mongodb = builder.AddMongoDB("mongodb")
    .WithDataVolume();
var chatDb = mongodb.AddDatabase("aspirechat");

// ============================================================
// Qdrant (Vector database for RAG)
// ============================================================
var qdrant = builder.AddQdrant("qdrant")
    .WithDataVolume();

// ============================================================
// Ollama LLM Service
// ============================================================
var ollama = builder.AddOllama("ollama")
    .WithGPUSupport()
    .WithDataVolume()
    .WithOpenWebUI();
var llava = ollama.AddModel("llava", "llava");       // Vision model (image understanding)
var llama = ollama.AddModel("llama", "llama3.1");    // Tool-calling model (function calling)
var embedding = ollama.AddModel("embedding", "nomic-embed-text"); // Embedding model (RAG)

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
    .WithReference(chatDb)
    .WithReference(qdrant)
    .WithReference(llava)
    .WithReference(llama)
    .WithReference(embedding)
    .WithReference(mcpServer)
    .WithReference(plannerAgent)
    .WithReference(reviewerAgent)
    .WithReference(researchAgent)
    .WithReference(codeAgent)
    .WaitFor(chatDb)
    .WaitFor(qdrant)
    .WaitFor(llava)
    .WaitFor(llama)
    .WaitFor(embedding)
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
    .WithEndpoint("http", e => e.Port = 5200)
    .WithEndpoint("https", e => e.Port = 7200)
    .WithHttpHealthCheck("/health")
    .WithReference(redis)
    .WaitFor(redis)
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
// Configure agents to connect to Ollama directly
// ============================================================
plannerAgent.WithReference(ollama).WaitFor(llama);
reviewerAgent.WithReference(ollama).WaitFor(llama);
researchAgent.WithReference(ollama).WaitFor(llama);
codeAgent.WithReference(ollama).WaitFor(llama);

// ============================================================
// YARP Gateway - Single entry point
// Routes configured in AspireOllama.Gateway/appsettings.json
// Supports Let's Encrypt TLS via LettuceEncrypt
// ============================================================
var gateway = builder.AddProject<Projects.AspireOllama_Gateway>("gateway")
    .WithReference(apiService)
    .WithReference(mcpServer)
    .WithReference(plannerAgent)
    .WithReference(reviewerAgent)
    .WithReference(researchAgent)
    .WithReference(codeAgent)
    .WithReference(webFrontend)
    .WithReference(scalar)
    .WithHttpHealthCheck("/health")
    .WaitFor(webFrontend)
    .WaitFor(apiService);

// Web frontend calls downstream services through the gateway
webFrontend.WithReference(gateway);

// ============================================================
// New Relic: Configure OTLP export for all services
// ============================================================
if (!string.IsNullOrWhiteSpace(newRelicLicenseKey))
{
    var services = new (IResourceBuilder<ProjectResource> resource, string name)[]
    {
        (gateway,        "AspireOllama.Gateway"),
        (apiService,     "AspireOllama.ApiService"),
        (webFrontend,    "AspireOllama.Web"),
        (mcpServer,      "AspireOllama.McpServer"),
        (plannerAgent,   "AspireOllama.A2A.PlannerAgent"),
        (reviewerAgent,  "AspireOllama.A2A.ReviewerAgent"),
        (researchAgent,  "AspireOllama.A2A.ResearchAgent"),
        (codeAgent,      "AspireOllama.A2A.CodeAgent"),
    };

    foreach (var (resource, name) in services)
    {
        resource
            .WithEnvironment("OTEL_SERVICE_NAME", name)
            .WithEnvironment("OTEL_Service_Namespace", otelServiceNamespace)
            .WithEnvironment("OTEL_Service_Version", otelServiceVersion)
            .WithEnvironment("OTEL_Deployment_Environment", otelDeploymentEnv)
            .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", newRelicOtlpEndpoint)
            .WithEnvironment("OTEL_EXPORTER_OTLP_HEADERS", $"api-key={newRelicLicenseKey}")
            .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf");
    }
}

builder.Build().Run();
