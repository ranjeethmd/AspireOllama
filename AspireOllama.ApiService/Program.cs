using AspireOllama.ApiService.Data;
using AspireOllama.ApiService.Services;
using AspireOllama.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// Configure longer timeout for all HTTP clients (including Ollama)
builder.Services.ConfigureHttpClientDefaults(http =>
{
    http.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(10));
});

// Register as a standard ChatClient with the llama model
builder.AddOllamaApiClient("llama")
    .AddChatClient();

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add SQLite database
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "chat.db");
builder.Services.AddDbContext<ChatDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Add chat history service
builder.Services.AddScoped<ChatHistoryService>();

// Add services to the container.
builder.Services.AddProblemDetails();

// Increase request body size limit for image uploads (50MB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024;
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler();
}

app.MapGet("/", () => "API service is running. Navigate to /chat with AI.");

// Test endpoint to verify Ollama is working
app.MapGet("/test-ollama", async (IChatClient chatClient, ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Testing Ollama connection...");
        var response = await chatClient.GetResponseAsync("Say hello in one word");
        return Results.Ok(new { success = true, response = response.Text });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Ollama test failed");
        return Results.Ok(new { success = false, error = ex.Message });
    }
});

// Session management endpoints
app.MapPost("/sessions", async (ChatHistoryService historyService) =>
{
    var session = await historyService.CreateSessionAsync();
    return Results.Ok(session);
})
.WithName("CreateSession");

app.MapGet("/sessions", async (ChatHistoryService historyService) =>
{
    var sessions = await historyService.GetSessionsAsync();
    return Results.Ok(sessions);
})
.WithName("GetSessions");

app.MapGet("/sessions/{id}", async (string id, ChatHistoryService historyService) =>
{
    var session = await historyService.GetSessionWithMessagesAsync(id);
    if (session == null)
        return Results.NotFound();
    return Results.Ok(session);
})
.WithName("GetSession");

app.MapDelete("/sessions/{id}", async (string id, ChatHistoryService historyService) =>
{
    var deleted = await historyService.DeleteSessionAsync(id);
    if (!deleted)
        return Results.NotFound();
    return Results.NoContent();
})
.WithName("DeleteSession");

// Chat endpoint with multimodal support
app.MapPost("/chat", async (IChatClient chatClient, ChatHistoryService historyService, ChatMessageRequest request, ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Chat request received for session {SessionId}", request.SessionId);

        // Get conversation history for context
        var history = await historyService.GetMessagesAsync(request.SessionId);

        // Build conversation messages
        var messages = new List<ChatMessage>();

        // Add history
        foreach (var msg in history)
        {
            var role = msg.Role == "user" ? ChatRole.User : ChatRole.Assistant;
            messages.Add(new ChatMessage(role, msg.Content));
        }

        // Build current message with potential images
        var contentParts = new List<AIContent>();

        // Add images first if present
        if (request.Images != null && request.Images.Count > 0)
        {
            logger.LogInformation("Processing {Count} images", request.Images.Count);
            foreach (var image in request.Images)
            {
                var imageBytes = Convert.FromBase64String(image.Base64Data);
                // Use explicit media type for better compatibility
                var mediaType = image.ContentType switch
                {
                    "image/jpeg" or "image/jpg" => "image/jpeg",
                    "image/png" => "image/png",
                    "image/gif" => "image/gif",
                    "image/webp" => "image/webp",
                    _ => "image/jpeg" // Default to JPEG
                };
                logger.LogInformation("Adding image: {FileName}, MediaType: {MediaType}, Size: {Size} bytes",
                    image.FileName, mediaType, imageBytes.Length);
                contentParts.Add(new DataContent(imageBytes, mediaType));
            }
        }

        // Add text content
        contentParts.Add(new TextContent(request.Content ?? "Describe this image"));

        var userMessage = new ChatMessage(ChatRole.User, contentParts);
        messages.Add(userMessage);

        // Save user message to history
        await historyService.AddMessageAsync(request.SessionId, "user", request.Content ?? "", request.Images);

        logger.LogInformation("Calling Ollama with {MessageCount} messages", messages.Count);

        // Get AI response
        var response = await chatClient.GetResponseAsync(messages);
        var responseText = response.Text ?? string.Empty;

        logger.LogInformation("Received response from Ollama");

        // Save assistant response to history
        await historyService.AddMessageAsync(request.SessionId, "assistant", responseText);

        return Results.Ok(new ChatMessageResponse
        {
            SessionId = request.SessionId,
            Response = responseText
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing chat request");
        return Results.Problem(
            detail: ex.Message,
            statusCode: 500,
            title: "Chat Error"
        );
    }
})
.WithName("Chat");

app.MapDefaultEndpoints();

app.Run();
