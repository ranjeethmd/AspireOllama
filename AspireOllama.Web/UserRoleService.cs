using AspireOllama.Shared;

namespace AspireOllama.Web;

/// <summary>
/// Caches the current user's roles from the API access token.
/// Scoped per Blazor circuit — fetched once per session.
/// </summary>
public class UserRoleService(ChatApiClient chatClient, ILogger<UserRoleService> logger)
{
    private UserInfo? _cachedProfile;
    private bool _loaded;

    public UserInfo Profile => _cachedProfile ?? new UserInfo();
    public bool IsLoaded => _loaded;

    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_loaded) return;

        try
        {
            _cachedProfile = await chatClient.GetMyProfileAsync(ct);
            logger.LogInformation("User roles loaded: {Roles}", string.Join(", ", _cachedProfile.Roles));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load user roles");
            _cachedProfile = new UserInfo();
        }

        _loaded = true;
    }
}
