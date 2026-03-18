namespace AspireOllama.ServiceDefaults.Authentication;

/// <summary>
/// Azure AD configuration options.
/// </summary>
public class AzureAdOptions
{
    public const string SectionName = "AzureAd";

    /// <summary>
    /// Azure AD instance URL (e.g., https://login.microsoftonline.com/).
    /// </summary>
    public string Instance { get; set; } = "https://login.microsoftonline.com/";

    /// <summary>
    /// Azure AD tenant ID.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Application (client) ID.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client secret for confidential client flows.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Application ID URI (audience for token validation).
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Callback path for OIDC sign-in.
    /// </summary>
    public string CallbackPath { get; set; } = "/signin-oidc";

    /// <summary>
    /// Callback path for sign-out.
    /// </summary>
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";

    /// <summary>
    /// Gets the Azure AD authority URL.
    /// </summary>
    public string Authority => $"{Instance.TrimEnd('/')}/{TenantId}/v2.0";
}

/// <summary>
/// Configuration for downstream API access via OBO flow.
/// </summary>
public class DownstreamApiOptions
{
    public const string SectionName = "DownstreamApis";

    /// <summary>
    /// MCP Server API configuration.
    /// </summary>
    public ApiConfig McpServer { get; set; } = new();

    /// <summary>
    /// A2A Agents API configuration.
    /// </summary>
    public ApiConfig A2AAgents { get; set; } = new();
}

/// <summary>
/// Configuration for a downstream API.
/// </summary>
public class ApiConfig
{
    /// <summary>
    /// Scopes required to call this API.
    /// </summary>
    public string[] Scopes { get; set; } = [];

    /// <summary>
    /// Base URL of the API (optional, uses service discovery if not set).
    /// </summary>
    public string? BaseUrl { get; set; }
}

/// <summary>
/// Client credentials configuration for service-to-service calls.
/// </summary>
public class ClientCredentialsOptions
{
    public const string SectionName = "AzureAd:ClientCredentials";

    /// <summary>
    /// Azure AD tenant ID.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Application (client) ID for this service.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client secret for this service.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The target audience (resource) for the token request.
    /// </summary>
    public string Audience { get; set; } = string.Empty;
}
