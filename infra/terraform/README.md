# Azure AD Terraform Setup

Creates both Azure AD app registrations, security groups, role assignments, and auto-generates `appsettings.Secrets.json` for all services.

## What Terraform Manages

- **AspireOllama** — API resource server with 1 delegated scope (`access_as_user`) + 29 App Roles
- **AspireOllama-Web** — Blazor frontend with `access_as_user` delegated permission + admin consent
- **4 Security Groups** — Read-Only Viewer, Standard User, Power User, Admin (with role assignments)
- **appsettings.Secrets.json** — Auto-generated config files for all 7 services (gitignored)

## Prerequisites

- [Terraform](https://www.terraform.io/downloads) >= 1.0
- Azure AD tenant with admin privileges
- `az login --tenant <your-tenant-id>`

## Usage

```bash
cd infra/terraform
cp terraform.tfvars.example terraform.tfvars
# Edit terraform.tfvars with your tenant ID

terraform init
terraform plan
terraform apply
```

After apply, Terraform writes `appsettings.Secrets.json` into each service project:

| Service | File |
|---|---|
| AspireOllama.ApiService | `appsettings.Secrets.json` |
| AspireOllama.McpServer | `appsettings.Secrets.json` |
| AspireOllama.Web | `appsettings.Secrets.json` |
| A2A PlannerAgent | `appsettings.Secrets.json` |
| A2A ReviewerAgent | `appsettings.Secrets.json` |
| A2A ResearchAgent | `appsettings.Secrets.json` |
| A2A CodeAgent | `appsettings.Secrets.json` |

These files are gitignored and loaded automatically by `AddServiceDefaults()`.

## After Apply

1. Assign users to security groups:
   **Azure Portal > Groups > AspireOllama - Standard User > Members > Add members**

2. Verify secrets (if needed):
   ```bash
   terraform output -raw api_client_secret
   terraform output -raw web_client_secret
   ```

## Variables

| Variable | Description | Default |
|---|---|---|
| `tenant_id` | Azure AD Tenant ID | *(required)* |
| `web_redirect_uris` | OIDC redirect URIs | `["https://localhost:7200/signin-oidc", "http://localhost:5200/signin-oidc"]` |
| `generate_appsettings` | Write appsettings.Secrets.json files | `true` |
| `project_root` | Path to solution root | `../..` |

## Disable Config Generation

If you prefer to manage secrets via `dotnet user-secrets` or Key Vault:

```bash
terraform apply -var="generate_appsettings=false"
```
