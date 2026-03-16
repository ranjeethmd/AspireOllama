using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using System.Text.Json;

namespace AspireOllama.Tests.UI;

/// <summary>
/// Tests for YARP Gateway routing and API endpoint accessibility.
/// These tests verify that all services are properly routed through the gateway.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class GatewayApiTests : PageTest
{
    private string _gatewayUrl = "http://localhost:7000";
    private IAPIRequestContext _apiContext = null!;

    [SetUp]
    public async Task Setup()
    {
        // Read gateway URL from environment variable if set
        var envUrl = Environment.GetEnvironmentVariable("GATEWAY_URL");
        if (!string.IsNullOrEmpty(envUrl))
        {
            _gatewayUrl = envUrl;
        }

        // Create API request context for direct HTTP calls
        _apiContext = await Playwright.APIRequest.NewContextAsync(new()
        {
            BaseURL = _gatewayUrl,
            IgnoreHTTPSErrors = true
        });
    }

    [TearDown]
    public async Task TearDown()
    {
        await _apiContext.DisposeAsync();
    }

    // ============================================================
    // Gateway Root Tests
    // ============================================================

    [Test]
    public async Task Gateway_ShouldBeAccessible()
    {
        var response = await _apiContext.GetAsync("/");
        Assert.That(response.Ok, Is.True, "Gateway should be accessible");
    }

    [Test]
    public async Task Gateway_WebFrontend_ShouldLoadHomePage()
    {
        await Page.GotoAsync(_gatewayUrl);

        // Wait for the page to load (Blazor app)
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The page should load without errors
        Assert.That(Page.Url, Does.StartWith(_gatewayUrl));
    }

    // ============================================================
    // API Service Tests (/api/*)
    // ============================================================

    [Test]
    public async Task Api_Root_ShouldReturnServiceInfo()
    {
        var response = await _apiContext.GetAsync("/api");
        Assert.That(response.Ok, Is.True, "API root should be accessible");

        var text = await response.TextAsync();
        Assert.That(text, Does.Contain("API service"));
    }

    [Test]
    public async Task Api_Sessions_CreateSession_ShouldSucceed()
    {
        var response = await _apiContext.PostAsync("/api/sessions");
        Assert.That(response.Ok, Is.True, $"Create session failed: {response.Status}");

        var json = await response.JsonAsync();
        Assert.That(json, Is.Not.Null);

        // Verify session has an ID
        var sessionId = json?.GetProperty("id").GetString();
        Assert.That(sessionId, Is.Not.Null.And.Not.Empty, "Session should have an ID");

        Console.WriteLine($"Created session: {sessionId}");
    }

    [Test]
    public async Task Api_Sessions_GetSessions_ShouldReturnList()
    {
        var response = await _apiContext.GetAsync("/api/sessions");
        Assert.That(response.Ok, Is.True, $"Get sessions failed: {response.Status}");

        var json = await response.JsonAsync();
        Assert.That(json?.ValueKind, Is.EqualTo(JsonValueKind.Array), "Should return array of sessions");
    }

    [Test]
    public async Task Api_Sessions_CreateAndRetrieve_ShouldWork()
    {
        // Create a session
        var createResponse = await _apiContext.PostAsync("/api/sessions");
        Assert.That(createResponse.Ok, Is.True);

        var createJson = await createResponse.JsonAsync();
        var sessionId = createJson?.GetProperty("id").GetString();
        Assert.That(sessionId, Is.Not.Null);

        // Retrieve the session
        var getResponse = await _apiContext.GetAsync($"/api/sessions/{sessionId}");
        Assert.That(getResponse.Ok, Is.True, $"Get session {sessionId} failed");

        var getJson = await getResponse.JsonAsync();
        var retrievedId = getJson?.GetProperty("session").GetProperty("id").GetString();
        Assert.That(retrievedId, Is.EqualTo(sessionId));
    }

    [Test]
    public async Task Api_Sessions_Delete_ShouldWork()
    {
        // Create a session
        var createResponse = await _apiContext.PostAsync("/api/sessions");
        var createJson = await createResponse.JsonAsync();
        var sessionId = createJson?.GetProperty("id").GetString();

        // Delete the session
        var deleteResponse = await _apiContext.DeleteAsync($"/api/sessions/{sessionId}");
        Assert.That(deleteResponse.Ok, Is.True, $"Delete session {sessionId} failed");

        // Verify it's deleted (should return 404)
        var getResponse = await _apiContext.GetAsync($"/api/sessions/{sessionId}");
        Assert.That(getResponse.Status, Is.EqualTo(404), "Deleted session should not be found");
    }

    [Test]
    public async Task Api_Agents_GetAgents_ShouldReturnList()
    {
        var response = await _apiContext.GetAsync("/api/agents");
        Assert.That(response.Ok, Is.True, $"Get agents failed: {response.Status}");

        var json = await response.JsonAsync();
        Assert.That(json?.ValueKind, Is.EqualTo(JsonValueKind.Array), "Should return array of agents");

        // Log agent count
        var count = json?.GetArrayLength() ?? 0;
        Console.WriteLine($"Found {count} agents");
    }

    // ============================================================
    // MCP Server Tests (/mcp/*)
    // ============================================================

    [Test]
    public async Task Mcp_Root_ShouldReturnServiceInfo()
    {
        var response = await _apiContext.GetAsync("/mcp");
        Assert.That(response.Ok, Is.True, $"MCP root failed: {response.Status}");

        var text = await response.TextAsync();
        Assert.That(text, Does.Contain("MCP"));
    }

    // ============================================================
    // A2A Agent Tests (/a2a/{agent}/*)
    // ============================================================

    [Test]
    public async Task A2A_Planner_Root_ShouldBeAccessible()
    {
        var response = await _apiContext.GetAsync("/a2a/planner");
        Assert.That(response.Ok, Is.True, $"Planner agent root failed: {response.Status}");

        var text = await response.TextAsync();
        Assert.That(text, Does.Contain("Planner"));
    }

    [Test]
    public async Task A2A_Planner_AgentCard_ShouldReturnJson()
    {
        var response = await _apiContext.GetAsync("/a2a/planner/.well-known/agent.json");
        Assert.That(response.Ok, Is.True, $"Planner agent card failed: {response.Status}");

        var json = await response.JsonAsync();
        Assert.That(json, Is.Not.Null, "Should return valid JSON");

        // Verify agent card structure
        var name = json?.GetProperty("name").GetString();
        Assert.That(name, Is.Not.Null.And.Not.Empty, "Agent card should have a name");
        Console.WriteLine($"Planner agent name: {name}");
    }

    [Test]
    public async Task A2A_Reviewer_AgentCard_ShouldReturnJson()
    {
        var response = await _apiContext.GetAsync("/a2a/reviewer/.well-known/agent.json");
        Assert.That(response.Ok, Is.True, $"Reviewer agent card failed: {response.Status}");

        var json = await response.JsonAsync();
        var name = json?.GetProperty("name").GetString();
        Assert.That(name, Is.Not.Null.And.Not.Empty);
        Console.WriteLine($"Reviewer agent name: {name}");
    }

    [Test]
    public async Task A2A_Research_AgentCard_ShouldReturnJson()
    {
        var response = await _apiContext.GetAsync("/a2a/research/.well-known/agent.json");
        Assert.That(response.Ok, Is.True, $"Research agent card failed: {response.Status}");

        var json = await response.JsonAsync();
        var name = json?.GetProperty("name").GetString();
        Assert.That(name, Is.Not.Null.And.Not.Empty);
        Console.WriteLine($"Research agent name: {name}");
    }

    [Test]
    public async Task A2A_Code_AgentCard_ShouldReturnJson()
    {
        var response = await _apiContext.GetAsync("/a2a/code/.well-known/agent.json");
        Assert.That(response.Ok, Is.True, $"Code agent card failed: {response.Status}");

        var json = await response.JsonAsync();
        var name = json?.GetProperty("name").GetString();
        Assert.That(name, Is.Not.Null.And.Not.Empty);
        Console.WriteLine($"Code agent name: {name}");
    }

    [Test]
    public async Task A2A_Planner_Tasks_ShouldReturnList()
    {
        var response = await _apiContext.GetAsync("/a2a/planner/a2a/tasks");
        Assert.That(response.Ok, Is.True, $"Planner tasks failed: {response.Status}");

        var json = await response.JsonAsync();
        Assert.That(json?.ValueKind, Is.EqualTo(JsonValueKind.Array), "Should return array of tasks");
    }

    // ============================================================
    // Health Check Tests
    // ============================================================

    [Test]
    public async Task Health_ApiService_ShouldBeHealthy()
    {
        var response = await _apiContext.GetAsync("/api/health");
        Assert.That(response.Ok, Is.True, $"API health check failed: {response.Status}");
    }

    // ============================================================
    // Integration Tests
    // ============================================================

    [Test]
    public async Task Integration_CreateSessionAndChat_WorkflowTest()
    {
        // 1. Create a session
        var createResponse = await _apiContext.PostAsync("/api/sessions");
        Assert.That(createResponse.Ok, Is.True, "Session creation failed");

        var sessionJson = await createResponse.JsonAsync();
        var sessionId = sessionJson?.GetProperty("id").GetString();
        Assert.That(sessionId, Is.Not.Null);
        Console.WriteLine($"Created session: {sessionId}");

        // 2. Get sessions list - should include our new session
        var listResponse = await _apiContext.GetAsync("/api/sessions");
        Assert.That(listResponse.Ok, Is.True);

        var sessions = await listResponse.JsonAsync();
        var found = false;
        foreach (var session in sessions?.EnumerateArray() ?? Enumerable.Empty<JsonElement>())
        {
            if (session.GetProperty("id").GetString() == sessionId)
            {
                found = true;
                break;
            }
        }
        Assert.That(found, Is.True, "Session should be in list");

        // 3. Clean up - delete the session
        var deleteResponse = await _apiContext.DeleteAsync($"/api/sessions/{sessionId}");
        Assert.That(deleteResponse.Ok, Is.True, "Session deletion failed");
    }

    [Test]
    public async Task Integration_AllAgentsAccessible_Test()
    {
        var agents = new[] { "planner", "reviewer", "research", "code" };

        foreach (var agent in agents)
        {
            // Test agent root
            var rootResponse = await _apiContext.GetAsync($"/a2a/{agent}");
            Assert.That(rootResponse.Ok, Is.True, $"Agent {agent} root should be accessible");

            // Test agent card
            var cardResponse = await _apiContext.GetAsync($"/a2a/{agent}/.well-known/agent.json");
            Assert.That(cardResponse.Ok, Is.True, $"Agent {agent} card should be accessible");

            // Test tasks endpoint
            var tasksResponse = await _apiContext.GetAsync($"/a2a/{agent}/a2a/tasks");
            Assert.That(tasksResponse.Ok, Is.True, $"Agent {agent} tasks should be accessible");

            Console.WriteLine($"Agent {agent}: All endpoints accessible");
        }
    }
}
