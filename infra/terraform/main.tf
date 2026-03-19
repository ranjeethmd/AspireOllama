terraform {
  required_providers {
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.0"
    }
    local = {
      source  = "hashicorp/local"
      version = "~> 2.0"
    }
  }
}

provider "azuread" {
  tenant_id = var.tenant_id
}

data "azuread_client_config" "current" {}

# =============================================================================
# Random UUIDs for App Role IDs and OAuth2 Permission Scope ID
# =============================================================================

resource "random_uuid" "scope_access_as_user" {}

resource "random_uuid" "role_api_access" {}
resource "random_uuid" "role_api_admin" {}
resource "random_uuid" "role_api_chat_read" {}
resource "random_uuid" "role_api_chat_write" {}
resource "random_uuid" "role_api_sessions_manage" {}
resource "random_uuid" "role_api_documents_manage" {}

resource "random_uuid" "role_mcp_access" {}
resource "random_uuid" "role_mcp_tools_get_time" {}
resource "random_uuid" "role_mcp_tools_get_weather" {}
resource "random_uuid" "role_mcp_tools_convert_units" {}

resource "random_uuid" "role_a2a_planner_access" {}
resource "random_uuid" "role_a2a_planner_create_plan" {}
resource "random_uuid" "role_a2a_planner_assess_complexity" {}
resource "random_uuid" "role_a2a_planner_suggest_agents" {}
resource "random_uuid" "role_a2a_planner_orchestrate_workflow" {}

resource "random_uuid" "role_a2a_reviewer_access" {}
resource "random_uuid" "role_a2a_reviewer_review_response" {}
resource "random_uuid" "role_a2a_reviewer_review_code" {}
resource "random_uuid" "role_a2a_reviewer_review_plan" {}
resource "random_uuid" "role_a2a_reviewer_provide_feedback" {}

resource "random_uuid" "role_a2a_research_access" {}
resource "random_uuid" "role_a2a_research_search_knowledge" {}
resource "random_uuid" "role_a2a_research_get_topic_details" {}
resource "random_uuid" "role_a2a_research_gather_context" {}
resource "random_uuid" "role_a2a_research_suggest_topics" {}

resource "random_uuid" "role_a2a_coordinator_access" {}
resource "random_uuid" "role_a2a_coordinator_orchestrate" {}

resource "random_uuid" "role_a2a_code_access" {}
resource "random_uuid" "role_a2a_code_execute_csharp" {}
resource "random_uuid" "role_a2a_code_generate_code" {}
resource "random_uuid" "role_a2a_code_analyze_code" {}
resource "random_uuid" "role_a2a_code_generate_tests" {}
resource "random_uuid" "role_a2a_code_refactor_code" {}

# =============================================================================
# App Registration: AspireOllama (API Resource Server)
# =============================================================================

resource "azuread_application" "api" {
  display_name     = "AspireOllama"
  sign_in_audience = "AzureADMyOrg"

  owners = [data.azuread_client_config.current.object_id]

  # ---------------------------------------------------------------------------
  # Delegated Scope: access_as_user (enables OBO token acquisition)
  # ---------------------------------------------------------------------------
  api {
    requested_access_token_version = 2

    oauth2_permission_scope {
      id                         = random_uuid.scope_access_as_user.result
      value                      = "access_as_user"
      type                       = "Admin"
      admin_consent_display_name = "Access AspireOllama as signed-in user"
      admin_consent_description  = "Allows the app to access AspireOllama APIs on behalf of the signed-in user"
      enabled                    = true
    }
  }

  # ---------------------------------------------------------------------------
  # App Roles (RBAC) — 31 roles
  # ---------------------------------------------------------------------------

  # ── API Service Roles ──
  app_role {
    id                   = random_uuid.role_api_access.result
    value                = "Api.Access"
    display_name         = "API Access"
    description          = "Coarse access to the API Service"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_api_chat_read.result
    value                = "Api.Chat.Read"
    display_name         = "Read Chat"
    description          = "Read chat messages and session history"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_api_chat_write.result
    value                = "Api.Chat.Write"
    display_name         = "Write Chat"
    description          = "Send chat messages and call agents"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_api_sessions_manage.result
    value                = "Api.Sessions.Manage"
    display_name         = "Manage Sessions"
    description          = "Create and delete chat sessions"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_api_admin.result
    value                = "Api.Admin"
    display_name         = "API Admin"
    description          = "Full administrative access including document management"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_api_documents_manage.result
    value                = "Api.Documents.Manage"
    display_name         = "Manage Documents"
    description          = "Upload and manage documents in the RAG vector store"
    allowed_member_types = ["User"]
    enabled              = true
  }

  # ── MCP Server Roles ──
  app_role {
    id                   = random_uuid.role_mcp_access.result
    value                = "Mcp.Access"
    display_name         = "MCP Access"
    description          = "Coarse access to MCP tools"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_mcp_tools_get_time.result
    value                = "Mcp.Tools.GetTime"
    display_name         = "Get Time"
    description          = "Use the time tool"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_mcp_tools_get_weather.result
    value                = "Mcp.Tools.GetWeather"
    display_name         = "Get Weather"
    description          = "Use the weather tool"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_mcp_tools_convert_units.result
    value                = "Mcp.Tools.ConvertUnits"
    display_name         = "Convert Units"
    description          = "Use the unit conversion tool"
    allowed_member_types = ["User"]
    enabled              = true
  }

  # ── Planner Agent Roles ──
  app_role {
    id                   = random_uuid.role_a2a_planner_access.result
    value                = "A2A.Planner.Access"
    display_name         = "Planner Access"
    description          = "Coarse access to Planner agent"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_a2a_planner_create_plan.result
    value                = "A2A.Planner.CreatePlan"
    display_name         = "Create Plan"
    description          = "Create task plans"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_a2a_planner_assess_complexity.result
    value                = "A2A.Planner.AssessComplexity"
    display_name         = "Assess Complexity"
    description          = "Assess task complexity"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_a2a_planner_suggest_agents.result
    value                = "A2A.Planner.SuggestAgents"
    display_name         = "Suggest Agents"
    description          = "Get agent suggestions for a task"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_a2a_planner_orchestrate_workflow.result
    value                = "A2A.Planner.OrchestrateWorkflow"
    display_name         = "Orchestrate Workflow"
    description          = "Run multi-agent workflows"
    allowed_member_types = ["User"]
    enabled              = true
  }

  # ── Reviewer Agent Roles ──
  app_role {
    id                   = random_uuid.role_a2a_reviewer_access.result
    value                = "A2A.Reviewer.Access"
    display_name         = "Reviewer Access"
    description          = "Coarse access to Reviewer agent"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_a2a_reviewer_review_response.result
    value                = "A2A.Reviewer.ReviewResponse"
    display_name         = "Review Response"
    description          = "Review AI responses"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_a2a_reviewer_review_code.result
    value                = "A2A.Reviewer.ReviewCode"
    display_name         = "Review Code"
    description          = "Review code"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_a2a_reviewer_review_plan.result
    value                = "A2A.Reviewer.ReviewPlan"
    display_name         = "Review Plan"
    description          = "Review plans"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_a2a_reviewer_provide_feedback.result
    value                = "A2A.Reviewer.ProvideFeedback"
    display_name         = "Provide Feedback"
    description          = "Get review feedback"
    allowed_member_types = ["User"]
    enabled              = true
  }

  # ── Research Agent Roles ──
  app_role {
    id                   = random_uuid.role_a2a_research_access.result
    value                = "A2A.Research.Access"
    display_name         = "Research Access"
    description          = "Coarse access to Research agent"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_a2a_research_search_knowledge.result
    value                = "A2A.Research.SearchKnowledge"
    display_name         = "Search Knowledge"
    description          = "Search knowledge base"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_a2a_research_get_topic_details.result
    value                = "A2A.Research.GetTopicDetails"
    display_name         = "Get Topic Details"
    description          = "Get detailed topic info"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_a2a_research_gather_context.result
    value                = "A2A.Research.GatherContext"
    display_name         = "Gather Context"
    description          = "Gather research context"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_a2a_research_suggest_topics.result
    value                = "A2A.Research.SuggestTopics"
    display_name         = "Suggest Topics"
    description          = "Get topic suggestions"
    allowed_member_types = ["User"]
    enabled              = true
  }

  # ── Coordinator Agent Roles ──
  app_role {
    id                   = random_uuid.role_a2a_coordinator_access.result
    value                = "A2A.Coordinator.Access"
    display_name         = "Coordinator Access"
    description          = "Access to Coordinator agent for multi-agent orchestration"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_a2a_coordinator_orchestrate.result
    value                = "A2A.Coordinator.Orchestrate"
    display_name         = "Orchestrate Task"
    description          = "Run multi-agent workflows via the Coordinator"
    allowed_member_types = ["User"]
    enabled              = true
  }

  # ── Code Agent Roles ──
  app_role {
    id                   = random_uuid.role_a2a_code_access.result
    value                = "A2A.Code.Access"
    display_name         = "Code Access"
    description          = "Coarse access to Code agent"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_a2a_code_execute_csharp.result
    value                = "A2A.Code.ExecuteCsharp"
    display_name         = "Execute C#"
    description          = "Execute C# code"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_a2a_code_generate_code.result
    value                = "A2A.Code.GenerateCode"
    display_name         = "Generate Code"
    description          = "Generate code"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_a2a_code_analyze_code.result
    value                = "A2A.Code.AnalyzeCode"
    display_name         = "Analyze Code"
    description          = "Analyze code"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_a2a_code_generate_tests.result
    value                = "A2A.Code.GenerateTests"
    display_name         = "Generate Tests"
    description          = "Generate test code"
    allowed_member_types = ["User"]
    enabled              = true
  }
  app_role {
    id                   = random_uuid.role_a2a_code_refactor_code.result
    value                = "A2A.Code.RefactorCode"
    display_name         = "Refactor Code"
    description          = "Refactor code"
    allowed_member_types = ["User"]
    enabled              = true
  }

  # Include "roles" in access tokens so backend services can read app roles
  optional_claims {
    access_token {
      name = "roles"
    }
  }

  # Microsoft Graph — User.Read (delegated, default permission)
  required_resource_access {
    resource_app_id = "00000003-0000-0000-c000-000000000000" # Microsoft Graph

    resource_access {
      id   = "e1fe6dd8-ba31-4d61-89e7-88639da4683d" # User.Read
      type = "Scope"
    }
  }

  web {
    implicit_grant {
      access_token_issuance_enabled = false
      id_token_issuance_enabled     = false
    }
  }
}

# ---------------------------------------------------------------------------
# Application ID URI: api://<client_id>
# lifecycle.prevent_destroy ensures Terraform never removes this.
# If it drifts, run: terraform apply -replace=azuread_application_identifier_uri.api
# ---------------------------------------------------------------------------
resource "azuread_application_identifier_uri" "api" {
  application_id = azuread_application.api.id
  identifier_uri = "api://${azuread_application.api.client_id}"

  lifecycle {
    replace_triggered_by = [azuread_application.api]
  }
}

# Service principal for the API app (required for role assignments)
# app_role_assignment_required = true ensures users must be explicitly assigned a role to get a token
# feature_tags.enterprise = true adds the "WindowsAzureActiveDirectoryIntegratedApp" tag
# which makes the app visible in Enterprise Applications (same as portal-created apps)
resource "azuread_service_principal" "api" {
  client_id                    = azuread_application.api.client_id
  app_role_assignment_required = true
  owners                       = [data.azuread_client_config.current.object_id]

  feature_tags {
    enterprise = true
  }
}

# Client secret for the API app
resource "azuread_application_password" "api" {
  application_id = azuread_application.api.id
  display_name   = "AspireOllama API Secret"
  end_date       = var.api_client_secret_end_date
}

# =============================================================================
# App Registration: AspireOllama-Web (Blazor Frontend)
# =============================================================================

resource "azuread_application" "web" {
  display_name     = "AspireOllama-Web"
  sign_in_audience = "AzureADMyOrg"

  owners = [data.azuread_client_config.current.object_id]

  # Authorization Code Flow with PKCE
  web {
    redirect_uris = var.web_redirect_uris

    implicit_grant {
      access_token_issuance_enabled = false
      id_token_issuance_enabled     = false
    }
  }

  # Delegated permission: access_as_user on the API app
  required_resource_access {
    resource_app_id = azuread_application.api.client_id

    resource_access {
      id   = random_uuid.scope_access_as_user.result
      type = "Scope"
    }
  }

  # Microsoft Graph — User.Read (delegated, default permission)
  required_resource_access {
    resource_app_id = "00000003-0000-0000-c000-000000000000" # Microsoft Graph

    resource_access {
      id   = "e1fe6dd8-ba31-4d61-89e7-88639da4683d" # User.Read
      type = "Scope"
    }
  }
}

# Service principal for the Web app
resource "azuread_service_principal" "web" {
  client_id                    = azuread_application.web.client_id
  app_role_assignment_required = true
  owners                       = [data.azuread_client_config.current.object_id]

  feature_tags {
    enterprise = true
  }
}

# Client secret for the Web app
resource "azuread_application_password" "web" {
  application_id = azuread_application.web.id
  display_name   = "AspireOllama Web Secret"
  end_date       = var.web_client_secret_end_date
}

# =============================================================================
# Admin Consent: Grant AspireOllama-Web the access_as_user scope
# =============================================================================

resource "azuread_service_principal_delegated_permission_grant" "web_to_api" {
  service_principal_object_id          = azuread_service_principal.web.object_id
  resource_service_principal_object_id = azuread_service_principal.api.object_id
  claim_values                         = ["access_as_user"]
}

# =============================================================================
# Microsoft 365 Security Groups — Persona-Based RBAC
# =============================================================================

resource "azuread_group" "viewer" {
  display_name     = "AspireOllama - Read-Only Viewer"
  description      = "Read-only access to chat and sessions"
  mail_enabled     = false
  security_enabled = true
  types            = []
  owners           = [data.azuread_client_config.current.object_id]
}

resource "azuread_group" "standard_user" {
  display_name     = "AspireOllama - Standard User"
  description      = "Chat, session management, and MCP tools access"
  mail_enabled     = false
  security_enabled = true
  types            = []
  owners           = [data.azuread_client_config.current.object_id]
}

resource "azuread_group" "power_user" {
  display_name     = "AspireOllama - Power User"
  description      = "Standard User access plus all A2A agents"
  mail_enabled     = false
  security_enabled = true
  types            = []
  owners           = [data.azuread_client_config.current.object_id]
}

resource "azuread_group" "admin" {
  display_name     = "AspireOllama - Admin"
  description      = "Full access to all 31 app roles"
  mail_enabled     = false
  security_enabled = true
  types            = []
  owners           = [data.azuread_client_config.current.object_id]
}

# =============================================================================
# Role Assignments — Read-Only Viewer
# Roles: Api.Chat.Read
# =============================================================================

resource "azuread_app_role_assignment" "viewer_api_chat_read" {
  app_role_id         = random_uuid.role_api_chat_read.result
  principal_object_id = azuread_group.viewer.object_id
  resource_object_id  = azuread_service_principal.api.object_id
}

# =============================================================================
# Role Assignments — Standard User
# Roles: Api.Access, Api.Chat.Read, Api.Chat.Write, Api.Sessions.Manage,
#         Mcp.Access, Mcp.Tools.GetTime, Mcp.Tools.GetWeather, Mcp.Tools.ConvertUnits
# =============================================================================

locals {
  standard_user_roles = {
    api_access              = random_uuid.role_api_access.result
    api_chat_read           = random_uuid.role_api_chat_read.result
    api_chat_write          = random_uuid.role_api_chat_write.result
    api_sessions_manage     = random_uuid.role_api_sessions_manage.result
    mcp_access             = random_uuid.role_mcp_access.result
    mcp_tools_get_time     = random_uuid.role_mcp_tools_get_time.result
    mcp_tools_get_weather  = random_uuid.role_mcp_tools_get_weather.result
    mcp_tools_convert_units = random_uuid.role_mcp_tools_convert_units.result
  }

  # Power User = Standard User + all A2A agent roles
  power_user_extra_roles = {
    a2a_planner_access              = random_uuid.role_a2a_planner_access.result
    a2a_planner_create_plan         = random_uuid.role_a2a_planner_create_plan.result
    a2a_planner_assess_complexity   = random_uuid.role_a2a_planner_assess_complexity.result
    a2a_planner_suggest_agents      = random_uuid.role_a2a_planner_suggest_agents.result
    a2a_planner_orchestrate_workflow = random_uuid.role_a2a_planner_orchestrate_workflow.result
    a2a_reviewer_access             = random_uuid.role_a2a_reviewer_access.result
    a2a_reviewer_review_response    = random_uuid.role_a2a_reviewer_review_response.result
    a2a_reviewer_review_code        = random_uuid.role_a2a_reviewer_review_code.result
    a2a_reviewer_review_plan        = random_uuid.role_a2a_reviewer_review_plan.result
    a2a_reviewer_provide_feedback   = random_uuid.role_a2a_reviewer_provide_feedback.result
    a2a_research_access             = random_uuid.role_a2a_research_access.result
    a2a_research_search_knowledge   = random_uuid.role_a2a_research_search_knowledge.result
    a2a_research_get_topic_details  = random_uuid.role_a2a_research_get_topic_details.result
    a2a_research_gather_context     = random_uuid.role_a2a_research_gather_context.result
    a2a_research_suggest_topics     = random_uuid.role_a2a_research_suggest_topics.result
    a2a_coordinator_access          = random_uuid.role_a2a_coordinator_access.result
    a2a_coordinator_orchestrate     = random_uuid.role_a2a_coordinator_orchestrate.result
    a2a_code_access                 = random_uuid.role_a2a_code_access.result
    a2a_code_execute_csharp         = random_uuid.role_a2a_code_execute_csharp.result
    a2a_code_generate_code          = random_uuid.role_a2a_code_generate_code.result
    a2a_code_analyze_code           = random_uuid.role_a2a_code_analyze_code.result
    a2a_code_generate_tests         = random_uuid.role_a2a_code_generate_tests.result
    a2a_code_refactor_code          = random_uuid.role_a2a_code_refactor_code.result
  }

  # Admin-only roles
  admin_only_roles = {
    api_admin              = random_uuid.role_api_admin.result
    api_documents_manage   = random_uuid.role_api_documents_manage.result
  }

  # Admin = all roles (Standard User + Power User extras + Admin only)
  all_roles = merge(local.standard_user_roles, local.power_user_extra_roles, local.admin_only_roles)
}

resource "azuread_app_role_assignment" "standard_user" {
  for_each = local.standard_user_roles

  app_role_id         = each.value
  principal_object_id = azuread_group.standard_user.object_id
  resource_object_id  = azuread_service_principal.api.object_id
}

# =============================================================================
# Role Assignments — Power User (Standard User roles + A2A agent roles)
# =============================================================================

resource "azuread_app_role_assignment" "power_user_standard" {
  for_each = local.standard_user_roles

  app_role_id         = each.value
  principal_object_id = azuread_group.power_user.object_id
  resource_object_id  = azuread_service_principal.api.object_id
}

resource "azuread_app_role_assignment" "power_user_a2a" {
  for_each = local.power_user_extra_roles

  app_role_id         = each.value
  principal_object_id = azuread_group.power_user.object_id
  resource_object_id  = azuread_service_principal.api.object_id
}

# =============================================================================
# Role Assignments — Admin (all 31 roles)
# =============================================================================

resource "azuread_app_role_assignment" "admin" {
  for_each = local.all_roles

  app_role_id         = each.value
  principal_object_id = azuread_group.admin.object_id
  resource_object_id  = azuread_service_principal.api.object_id
}

# =============================================================================
# Generated Application Configuration (appsettings.Secrets.json)
# Terraform writes real Azure AD values into each service project.
# These files are gitignored and loaded by ASP.NET Core configuration.
# =============================================================================

resource "random_password" "gateway_shared_secret" {
  length  = 44
  special = false
}

locals {
  audience        = "api://${azuread_application.api.client_id}"
  instance        = "https://login.microsoftonline.com/"
  gateway_secret  = random_password.gateway_shared_secret.result

  # Backend services share the API app registration
  backend_azure_ad = {
    AzureAd = {
      Instance     = local.instance
      TenantId     = var.tenant_id
      ClientId     = azuread_application.api.client_id
      ClientSecret = azuread_application_password.api.value
      Audience     = local.audience
    }
    Gateway = {
      SharedSecret = local.gateway_secret
    }
  }

  # Web frontend uses the Web app registration
  web_config = {
    AzureAd = {
      Instance              = local.instance
      TenantId              = var.tenant_id
      ClientId              = azuread_application.web.client_id
      ClientSecret          = azuread_application_password.web.value
      Audience              = local.audience
      CallbackPath          = "/signin-oidc"
      SignedOutCallbackPath = "/signout-callback-oidc"
    }
    Gateway = {
      SharedSecret = local.gateway_secret
    }
    DownstreamApis = {
      ApiService = {
        BaseUrl = "https+http://gateway"
        Scopes  = ["${local.audience}/.default"]
      }
      McpServer = {
        BaseUrl = "https+http://gateway"
        Scopes  = ["${local.audience}/.default"]
      }
      A2AAgents = {
        BaseUrl = "https+http://gateway"
        Scopes  = ["${local.audience}/.default"]
      }
    }
  }

  # All backend service paths (relative to project_root)
  backend_services = var.generate_appsettings ? {
    api      = "AspireOllama.ApiService"
    mcp      = "AspireOllama.McpServer"
    planner  = "A2A/AspireOllama.A2A.PlannerAgent"
    reviewer = "A2A/AspireOllama.A2A.ReviewerAgent"
    research = "A2A/AspireOllama.A2A.ResearchAgent"
    code        = "A2A/AspireOllama.A2A.CodeAgent"
    coordinator = "A2A/AspireOllama.A2A.CoordinatorAgent"
  } : {}
}

resource "local_sensitive_file" "backend_secrets" {
  for_each = local.backend_services

  filename = "${var.project_root}/${each.value}/appsettings.Secrets.json"
  content  = jsonencode(local.backend_azure_ad)
}

resource "local_sensitive_file" "web_secrets" {
  count = var.generate_appsettings ? 1 : 0

  filename = "${var.project_root}/AspireOllama.Web/appsettings.Secrets.json"
  content  = jsonencode(local.web_config)
}
