# AspireOllama UI Tests (Playwright)

This project contains Playwright UI tests for testing the A2A Agents page.

## Prerequisites

1. .NET 10.0+ SDK
2. The AspireOllama application running

## Setup

### 1. Install Playwright browsers

```bash
cd AspireOllama.Tests.UI
dotnet build
pwsh bin/Debug/net10.0/playwright.ps1 install
```

Or on first test run, browsers will be installed automatically.

### 2. Start the application

In a separate terminal:
```bash
cd AspireOllama
dotnet run --project AspireOllama.AppHost
```

Note the web frontend URL (e.g., `https://localhost:7170`)

### 3. Set the application URL

Set the environment variable with your app URL:

**PowerShell:**
```powershell
$env:APP_URL = "https://localhost:7170"
```

**Command Prompt:**
```cmd
set APP_URL=https://localhost:7170
```

**Bash:**
```bash
export APP_URL="https://localhost:7170"
```

## Running Tests

### Run all tests (headless)
```bash
dotnet test
```

### Run all tests (with browser visible)
```bash
dotnet test -- Playwright.LaunchOptions.Headless=false
```

### Run all tests with slow motion (easier to watch)
```bash
dotnet test --settings .runsettings
```

### Run specific test class
```bash
dotnet test --filter "FullyQualifiedName~AgentsPageTests"
dotnet test --filter "FullyQualifiedName~AgentsWorkflowTests"
dotnet test --filter "FullyQualifiedName~ChatPageTests"
dotnet test --filter "FullyQualifiedName~GatewayApiTests"
```

### Run a specific test
```bash
dotnet test --filter "Name=AgentsPage_ShouldLoad"
```

## Test Files

| File | Description |
|------|-------------|
| `AgentsPageTests.cs` | Basic page load and navigation tests |
| `AgentsWorkflowTests.cs` | Tests for the Workflow tab and sequence diagram |
| `ChatPageTests.cs` | Tests for the Chat page |
| `GatewayApiTests.cs` | Tests for Gateway API routing |
| `GlobalSetup.cs` | One-time setup to install Playwright browsers |

## What the tests verify

### AgentsPageTests
- Page loads correctly
- All 5 agents are displayed (Coordinator, Planner, Reviewer, Research, Code)
- Tool Tester and Workflow tabs exist
- Navigation back to Chat works
- Refresh button exists

### AgentsWorkflowTests
- Running a workflow shows sequence diagram
- Agent names are shown for each step
- Step numbers are in sequence
- Execution times are displayed
- Results can be expanded
- Call summary shows agent statistics

### ChatPageTests
- Chat page loads correctly
- Session management works

### GatewayApiTests
- Gateway routes requests correctly to backend services

## Troubleshooting

### Browsers not installed
Run:
```bash
pwsh bin/Debug/net10.0/playwright.ps1 install
```

### Connection refused errors
Make sure the AspireOllama application is running:
```bash
dotnet run --project AspireOllama.AppHost
```

### Timeout errors
- Increase timeout in tests if agents are slow to respond
- Check that agents are healthy in Aspire dashboard

### SSL certificate errors
The tests use `https://localhost` which may have self-signed certificates. Playwright handles this automatically.
