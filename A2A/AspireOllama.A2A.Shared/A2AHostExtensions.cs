using AspireOllama.A2A.Protocol;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AspireOllama.A2A.Shared;

/// <summary>
/// Extension methods for A2A protocol registration and endpoint mapping.
/// </summary>
public static class A2AHostExtensions
{
    /// <summary>
    /// Registers A2A protocol services: known agents from config, HTTP clients, and IA2AAgentClient.
    /// </summary>
    public static WebApplicationBuilder AddA2AServices(this WebApplicationBuilder builder)
    {
        var knownAgents = new Dictionary<string, string>();
        var section = builder.Configuration.GetSection("A2A:KnownAgents");
        foreach (var child in section.GetChildren())
        {
            knownAgents[child.Key] = child.Value ?? $"http://{child.Key}-agent";
        }

        builder.Services.Configure<KnownAgentsOptions>(options => options.Agents = knownAgents);

        foreach (var (name, url) in knownAgents)
        {
            builder.Services.AddHttpClient(name, client => client.BaseAddress = new Uri(url));
        }

        builder.Services.AddSingleton<IA2AAgentClient, A2AAgentClient>();

        // Per-user rate limiting for A2A endpoints
        builder.AddA2ARateLimiting();

        return builder;
    }

    /// <summary>
    /// Registers the A2A server implementation as a singleton.
    /// </summary>
    public static WebApplicationBuilder AddA2AServer<TServer>(this WebApplicationBuilder builder)
        where TServer : class, IA2AServer
    {
        builder.Services.AddSingleton<TServer>();
        return builder;
    }

    /// <summary>
    /// Maps all A2A protocol endpoints per the specification.
    /// Agent card is always unauthenticated (discovery).
    /// All other endpoints require accessRole if provided.
    /// Per-skill authorization via <see cref="ISkillAuthorizationProvider"/> when the server implements it.
    /// Unsupported operations return 501 Not Implemented.
    /// </summary>
    public static WebApplication MapA2AEndpoints<TServer>(this WebApplication app, string? accessRole = null)
        where TServer : class, IA2AServer
    {
        app.UseA2ARateLimiting();

        var server = app.Services.GetRequiredService<TServer>();
        AuthorizeAttribute? auth = !string.IsNullOrWhiteSpace(accessRole)
            ? new AuthorizeAttribute { Roles = accessRole }
            : null;

        // ── Discovery (unauthenticated) ──
        app.MapGet("/.well-known/agent.json", () => server.GetAgentCard());

        var a2aLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("A2A.Authorization");

        // ── Core: message/send (3.1.1) — with per-skill authorization ──
        Secure(app.MapPost("/a2a/message:send", async (HttpContext httpContext, SendMessageRequest request, CancellationToken ct) =>
        {
            if (IsSkillForbidden(server, httpContext, request.Message))
            {
                var userId = httpContext.User.FindFirst("oid")?.Value ?? "unknown";
                var skill = (server as ISkillAuthorizationProvider)?.ResolveSkill(request.Message);
                a2aLogger.LogWarning("A2A skill denied: user={UserId}, skill={Skill}, endpoint=message:send", userId, skill);
                return Results.Forbid();
            }

            return Results.Ok(await server.HandleSendMessageAsync(request, ct));
        }), auth);

        // ── Core: message/stream (3.1.2) — with per-skill authorization ──
        Secure(app.MapPost("/a2a/message:stream", (HttpContext httpContext, SendMessageRequest request, CancellationToken ct) =>
        {
            if (IsSkillForbidden(server, httpContext, request.Message))
            {
                var userId = httpContext.User.FindFirst("oid")?.Value ?? "unknown";
                var skill = (server as ISkillAuthorizationProvider)?.ResolveSkill(request.Message);
                a2aLogger.LogWarning("A2A skill denied: user={UserId}, skill={Skill}, endpoint=message:stream", userId, skill);
                return Results.Forbid();
            }

            return SafeCall(() => Results.Ok(server.HandleSendMessageStreamAsync(request, ct)));
        }), auth);

        // ── Core: tasks/get (3.1.3) ──
        Secure(app.MapGet("/a2a/tasks/{taskId}", (string taskId) =>
            server.GetTask(taskId) is { } task ? Results.Ok(task) : Results.NotFound()), auth);

        // ── Core: tasks/list (3.1.4) ──
        Secure(app.MapGet("/a2a/tasks", () => server.GetAllTasks()), auth);

        // ── Core: tasks/cancel (3.1.5) ──
        Secure(app.MapPost("/a2a/tasks/{taskId}:cancel", (string taskId) =>
            server.CancelTask(taskId) ? Results.Ok() : Results.NotFound()), auth);

        // ── Core: tasks/subscribe (3.1.6) ──
        Secure(app.MapPost("/a2a/tasks/{taskId}:subscribe", (string taskId, CancellationToken ct) =>
            SafeCall(() => Results.Ok(server.SubscribeToTaskAsync(taskId, ct)))), auth);

        // ── Push Notifications: create (3.1.7) ──
        Secure(app.MapPost("/a2a/tasks/{taskId}/pushNotification", async (string taskId, PushNotificationRequest req, CancellationToken ct) =>
            await SafeCallAsync(async () => Results.Ok(await server.CreatePushNotificationAsync(taskId, req.WebhookUrl, ct)))), auth);

        // ── Push Notifications: get (3.1.8) ──
        Secure(app.MapGet("/a2a/tasks/{taskId}/pushNotification/{configId}", async (string taskId, string configId, CancellationToken ct) =>
            await SafeCallAsync(async () => await server.GetPushNotificationAsync(taskId, configId, ct) is { } config
                ? Results.Ok(config) : Results.NotFound())), auth);

        // ── Push Notifications: list (3.1.9) ──
        Secure(app.MapGet("/a2a/tasks/{taskId}/pushNotification", async (string taskId, CancellationToken ct) =>
            await SafeCallAsync(async () => Results.Ok(await server.ListPushNotificationsAsync(taskId, ct)))), auth);

        // ── Push Notifications: delete (3.1.10) ──
        Secure(app.MapDelete("/a2a/tasks/{taskId}/pushNotification/{configId}", async (string taskId, string configId, CancellationToken ct) =>
            await SafeCallAsync(async () => await server.DeletePushNotificationAsync(taskId, configId, ct)
                ? Results.Ok() : Results.NotFound())), auth);

        // ── Extended: agent/card (3.1.11) ──
        Secure(app.MapGet("/a2a/agent/card", (string? tenantId) =>
            server.GetExtendedAgentCard(tenantId)), auth);

        // ── Root ──
        var card = server.GetAgentCard();
        app.MapGet("/", () => $"{card.Name} is running. Endpoint: /.well-known/agent.json");

        return app;
    }

    private static void Secure(RouteHandlerBuilder endpoint, AuthorizeAttribute? auth)
    {
        if (auth is not null)
        {
            endpoint.RequireAuthorization(auth);
            endpoint.RequireRateLimiting(A2ARateLimitExtensions.PolicyName);
        }
    }

    private static bool IsSkillForbidden(IA2AServer server, HttpContext httpContext, Message message)
    {
        if (server is not ISkillAuthorizationProvider provider)
            return false;

        var skillId = provider.ResolveSkill(message);
        if (skillId is null)
            return true; // Deny by default when skill cannot be resolved

        if (!provider.GetSkillRoles().TryGetValue(skillId, out var requiredRole))
            return true; // Deny by default when skill has no role mapping

        return !httpContext.User.IsInRole(requiredRole);
    }

    private static IResult SafeCall(Func<IResult> action)
    {
        try { return action(); }
        catch (NotSupportedException) { return Results.StatusCode(501); }
    }

    private static async Task<IResult> SafeCallAsync(Func<Task<IResult>> action)
    {
        try { return await action(); }
        catch (NotSupportedException) { return Results.StatusCode(501); }
    }
}

/// <summary>
/// Request body for creating push notification config.
/// </summary>
public class PushNotificationRequest
{
    public string WebhookUrl { get; set; } = "";
}
