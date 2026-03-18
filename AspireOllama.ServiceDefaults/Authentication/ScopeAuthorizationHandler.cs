// RBAC is handled by ASP.NET Core's built-in role-based authorization.
// Azure AD App Roles map to ClaimTypes.Role via RoleClaimType = "roles" in JWT config.
// Use [Authorize(Roles = "...")] or Roles("...") in FastEndpoints.
// No custom authorization handler is needed.
