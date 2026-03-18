variable "tenant_id" {
  description = "Azure AD Tenant ID"
  type        = string
}

variable "web_redirect_uris" {
  description = "Redirect URIs for the Web frontend (Authorization Code Flow with PKCE)"
  type        = list(string)
  default = [
    "https://localhost:7200/signin-oidc",
    "http://localhost:5200/signin-oidc"
  ]
}

variable "api_client_secret_end_date" {
  description = "Expiry date for the AspireOllama API client secret (RFC3339)"
  type        = string
  default     = "2026-12-31T23:59:59Z"
}

variable "web_client_secret_end_date" {
  description = "Expiry date for the AspireOllama-Web client secret (RFC3339)"
  type        = string
  default     = "2026-12-31T23:59:59Z"
}

variable "project_root" {
  description = "Absolute path to the AspireOllama solution root (for generating appsettings files)"
  type        = string
  default     = "../.."
}

variable "generate_appsettings" {
  description = "When true, Terraform writes appsettings.Secrets.json files with real Azure AD values"
  type        = bool
  default     = true
}
