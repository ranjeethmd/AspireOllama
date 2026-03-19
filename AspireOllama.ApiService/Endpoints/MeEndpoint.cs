using AspireOllama.Shared;
using FastEndpoints;
using static AspireOllama.ServiceDefaults.Authentication.AuthRoles;

namespace AspireOllama.ApiService.Endpoints;

/// <summary>
/// Returns the authenticated user's profile and roles from the access token.
/// Used by the Blazor frontend to make role-based UI decisions.
/// </summary>
public class MeEndpoint(ILogger<MeEndpoint> logger) : EndpointWithoutRequest<UserInfo>
{
    public override void Configure()
    {
        Get("/me");
        Roles(ApiAccess, ApiChatRead, ApiChatWrite, ApiAdmin, ApiDocumentsManage, ApiSessionsManage);
        Summary(s =>
        {
            s.Summary = "Get current user profile and roles";
            s.Description = "Returns the authenticated user's identity and app roles from the access token";
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = HttpContext.User;

        var roles = user.FindAll("roles")
            .Concat(user.FindAll(System.Security.Claims.ClaimTypes.Role))
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var userInfo = new UserInfo
        {
            UserId = user.FindFirst("oid")?.Value
                ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? "unknown",
            UserName = user.FindFirst("name")?.Value
                ?? user.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                ?? "Unknown",
            Email = user.FindFirst("preferred_username")?.Value
                ?? user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
            Roles = roles
        };

        logger.LogInformation("User {UserId} ({UserName}) has roles: {Roles}",
            userInfo.UserId, userInfo.UserName, string.Join(", ", roles));

        await Send.OkAsync(userInfo, ct);
    }
}
