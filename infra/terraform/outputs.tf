# =============================================================================
# AspireOllama API App
# =============================================================================

output "api_application_id" {
  description = "Application (client) ID for AspireOllama"
  value       = azuread_application.api.client_id
}

output "api_object_id" {
  description = "Object ID for AspireOllama app registration"
  value       = azuread_application.api.object_id
}

output "api_identifier_uri" {
  description = "Application ID URI (audience) — api://<client-id>"
  value       = azuread_application_identifier_uri.api.identifier_uri
}

output "api_client_secret" {
  description = "Client secret for AspireOllama (store in Key Vault)"
  value       = azuread_application_password.api.value
  sensitive   = true
}

# =============================================================================
# AspireOllama-Web App
# =============================================================================

output "web_application_id" {
  description = "Application (client) ID for AspireOllama-Web"
  value       = azuread_application.web.client_id
}

output "web_object_id" {
  description = "Object ID for AspireOllama-Web app registration"
  value       = azuread_application.web.object_id
}

output "web_client_secret" {
  description = "Client secret for AspireOllama-Web (store in Key Vault)"
  value       = azuread_application_password.web.value
  sensitive   = true
}

# =============================================================================
# Configuration values for appsettings.json
# =============================================================================

output "appsettings_backend" {
  description = "AzureAd config for Gateway + all backend services"
  value = {
    AzureAd = {
      Instance     = "https://login.microsoftonline.com/"
      TenantId     = var.tenant_id
      ClientId     = azuread_application.api.client_id
      ClientSecret = "*** use: terraform output -raw api_client_secret ***"
      Audience     = azuread_application_identifier_uri.api.identifier_uri
    }
  }
}

output "appsettings_web" {
  description = "AzureAd config for Web frontend"
  value = {
    AzureAd = {
      Instance     = "https://login.microsoftonline.com/"
      TenantId     = var.tenant_id
      ClientId     = azuread_application.web.client_id
      ClientSecret = "*** use: terraform output -raw web_client_secret ***"
      Audience     = azuread_application_identifier_uri.api.identifier_uri
    }
  }
}

# =============================================================================
# Security Groups
# =============================================================================

output "group_viewer_id" {
  description = "Object ID for AspireOllama - Read-Only Viewer security group"
  value       = azuread_group.viewer.object_id
}

output "group_standard_user_id" {
  description = "Object ID for AspireOllama - Standard User security group"
  value       = azuread_group.standard_user.object_id
}

output "group_power_user_id" {
  description = "Object ID for AspireOllama - Power User security group"
  value       = azuread_group.power_user.object_id
}

output "group_admin_id" {
  description = "Object ID for AspireOllama - Admin security group"
  value       = azuread_group.admin.object_id
}

output "security_groups" {
  description = "All security groups with their object IDs"
  value = {
    "Read-Only Viewer" = azuread_group.viewer.object_id
    "Standard User"    = azuread_group.standard_user.object_id
    "Power User"       = azuread_group.power_user.object_id
    "Admin"            = azuread_group.admin.object_id
  }
}

# =============================================================================
# Gateway Shared Secret
# =============================================================================

output "gateway_shared_secret" {
  description = "Gateway shared secret for X-Gateway-Auth header (auto-generated)"
  value       = random_password.gateway_shared_secret.result
  sensitive   = true
}

# =============================================================================
# Generated Config Files
# =============================================================================

output "generated_config_files" {
  description = "Paths to Terraform-generated appsettings.Secrets.json files"
  value = var.generate_appsettings ? concat(
    [for k, v in local.backend_services : "${var.project_root}/${v}/appsettings.Secrets.json"],
    ["${var.project_root}/AspireOllama.Web/appsettings.Secrets.json"]
  ) : []
}
