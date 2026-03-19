namespace AspireOllama.Shared;

public class UserInfo
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string[] Roles { get; set; } = [];

    public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    public bool IsAdmin => HasRole("Api.Admin");
    public bool CanManageDocuments => IsAdmin || HasRole("Api.Documents.Manage");
}
