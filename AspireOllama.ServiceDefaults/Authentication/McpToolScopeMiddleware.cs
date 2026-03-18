using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace AspireOllama.ServiceDefaults.Authentication;

/// <summary>
/// Middleware that intercepts MCP tools/call JSON-RPC requests
/// and enforces per-tool RBAC role authorization via User.IsInRole().
/// </summary>
public class McpToolRoleMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<McpToolRoleMiddleware> _logger;

    public McpToolRoleMiddleware(RequestDelegate next, ILogger<McpToolRoleMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
            !context.Request.Path.StartsWithSegments("/mcp"))
        {
            await _next(context);
            return;
        }

        context.Request.EnableBuffering();
        var body = await new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
        context.Request.Body.Position = 0;

        string? toolName = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("method", out var methodProp) &&
                methodProp.GetString() is "tools/call")
            {
                if (root.TryGetProperty("params", out var paramsProp) &&
                    paramsProp.TryGetProperty("name", out var nameProp))
                {
                    toolName = nameProp.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // Not valid JSON or not a tool call — pass through
        }

        if (toolName is not null)
        {
            if (!AuthRoles.McpToolRoles.TryGetValue(toolName, out var requiredRole))
            {
                _logger.LogWarning("Unknown MCP tool: {Tool}", toolName);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    jsonrpc = "2.0",
                    error = new { code = -32600, message = $"Unknown tool: {toolName}" }
                });
                return;
            }

            if (!context.User.IsInRole(requiredRole))
            {
                _logger.LogWarning("MCP tool denied: {Tool} requires role {Role}", toolName, requiredRole);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    jsonrpc = "2.0",
                    error = new { code = -32600, message = $"Insufficient role. Required: {requiredRole}" }
                });
                return;
            }
        }

        await _next(context);
    }
}
