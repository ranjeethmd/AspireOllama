var builder = DistributedApplication.CreateBuilder(args);

var ollama = builder.AddOllama("ollama")
    .WithGPUSupport()
    .WithDataVolume()
    .WithOpenWebUI();
var llama = ollama.AddModel("llama", "llava");

var apiService = builder.AddProject<Projects.AspireOllama_ApiService>("apiservice")
    .WithUrl("/scalar/v1")
    .WithReference(llama)
    .WaitFor(llama)
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.AspireOllama_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
