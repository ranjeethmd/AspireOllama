using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using System.Text.Json;

namespace AspireOllama.Tests.UI;

/// <summary>
/// API tests via Playwright APIRequestContext — verifies gateway routing,
/// session CRUD, A2A agent discovery (all 5 agents), and health endpoints.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class GatewayApiTests : PageTest
{
    private string _baseUrl = "https://localhost:7170";
    private IAPIRequestContext _api = null!;

    private static readonly string[] AllAgents = ["coordinator", "planner", "reviewer", "research", "code"];

    [SetUp]
    public async Task Setup()
    {
        var envUrl = Environment.GetEnvironmentVariable("APP_URL")
                  ?? Environment.GetEnvironmentVariable("GATEWAY_URL");
        if (!string.IsNullOrEmpty(envUrl))
            _baseUrl = envUrl;

        _api = await Playwright.APIRequest.NewContextAsync(new()
        {
            BaseURL = _baseUrl,
            IgnoreHTTPSErrors = true
        });
    }

    [TearDown]
    public async Task TearDown() => await _api.DisposeAsync();

    // ── Gateway Root ──

    [Test]
    public async Task Gateway_Root_ShouldBeAccessible()
    {
        var response = await _api.GetAsync("/");
        Assert.That(response.Ok, Is.True, "Gateway root should respond 200");
    }

    // ── Session CRUD ──

    [Test]
    public async Task Sessions_Create_ShouldReturnId()
    {
        var response = await _api.PostAsync("/api/sessions");
        Assert.That(response.Ok, Is.True, $"Create session failed: {response.Status}");

        var json = await response.JsonAsync();
        var id = json?.GetProperty("id").GetString();
        Assert.That(id, Is.Not.Null.And.Not.Empty);
        Console.WriteLine($"Created session: {id}");
    }

    [Test]
    public async Task Sessions_List_ShouldReturnArray()
    {
        var response = await _api.GetAsync("/api/sessions");
        Assert.That(response.Ok, Is.True);

        var json = await response.JsonAsync();
        Assert.That(json?.ValueKind, Is.EqualTo(JsonValueKind.Array));
    }

    [Test]
    public async Task Sessions_CreateAndRetrieve_ShouldMatch()
    {
        var createRes = await _api.PostAsync("/api/sessions");
        var createJson = await createRes.JsonAsync();
        var sessionId = createJson?.GetProperty("id").GetString();

        var getRes = await _api.GetAsync($"/api/sessions/{sessionId}");
        Assert.That(getRes.Ok, Is.True);

        var getJson = await getRes.JsonAsync();
        var retrievedId = getJson?.GetProperty("session").GetProperty("id").GetString();
        Assert.That(retrievedId, Is.EqualTo(sessionId));
    }

    [Test]
    public async Task Sessions_Delete_ShouldSucceed()
    {
        var createRes = await _api.PostAsync("/api/sessions");
        var id = (await createRes.JsonAsync())?.GetProperty("id").GetString();

        var deleteRes = await _api.DeleteAsync($"/api/sessions/{id}");
        Assert.That(deleteRes.Ok, Is.True);

        var getRes = await _api.GetAsync($"/api/sessions/{id}");
        Assert.That(getRes.Status, Is.EqualTo(404));
    }

    [Test]
    public async Task Sessions_GetNonExistent_ShouldReturn404()
    {
        var response = await _api.GetAsync("/api/sessions/non-existent-id");
        Assert.That(response.Status, Is.EqualTo(404));
    }

    // ── Agents API ──

    [Test]
    public async Task Agents_List_ShouldReturnArray()
    {
        var response = await _api.GetAsync("/api/agents");
        Assert.That(response.Ok, Is.True);

        var json = await response.JsonAsync();
        Assert.That(json?.ValueKind, Is.EqualTo(JsonValueKind.Array));

        var count = json?.GetArrayLength() ?? 0;
        Console.WriteLine($"Found {count} agents");
    }

    // ── A2A Agent Discovery (all 5) ──

    [TestCaseSource(nameof(AllAgents))]
    public async Task A2A_AgentRoot_ShouldBeAccessible(string agent)
    {
        var response = await _api.GetAsync($"/a2a/{agent}");
        Assert.That(response.Ok, Is.True, $"{agent} root should respond 200");
    }

    [TestCaseSource(nameof(AllAgents))]
    public async Task A2A_AgentCard_ShouldReturnValidJson(string agent)
    {
        var response = await _api.GetAsync($"/a2a/{agent}/.well-known/agent.json");
        Assert.That(response.Ok, Is.True, $"{agent} agent card should respond 200");

        var json = await response.JsonAsync();
        var name = json?.GetProperty("name").GetString();
        Assert.That(name, Is.Not.Null.And.Not.Empty, $"{agent} card should have a name");

        var skills = json?.GetProperty("skills").GetArrayLength() ?? 0;
        Assert.That(skills, Is.GreaterThan(0), $"{agent} should have at least one skill");

        Console.WriteLine($"{agent}: name={name}, skills={skills}");
    }

    [TestCaseSource(nameof(AllAgents))]
    public async Task A2A_Tasks_ShouldReturnArray(string agent)
    {
        var response = await _api.GetAsync($"/a2a/{agent}/a2a/tasks");
        Assert.That(response.Ok, Is.True, $"{agent} tasks should respond 200");

        var json = await response.JsonAsync();
        Assert.That(json?.ValueKind, Is.EqualTo(JsonValueKind.Array));
    }

    // ── Health Checks ──

    [Test]
    public async Task Health_Alive_ShouldRespond()
    {
        var response = await _api.GetAsync("/alive");
        // Aspire health endpoints return 200
        Assert.That(response.Status, Is.LessThan(500), "Alive endpoint should not error");
    }

    [Test]
    public async Task Health_Ready_ShouldRespond()
    {
        var response = await _api.GetAsync("/health");
        Assert.That(response.Status, Is.LessThan(500), "Health endpoint should not error");
    }

    // ── Integration ──

    [Test]
    public async Task Integration_CreateChatDeleteSession()
    {
        // Create
        var createRes = await _api.PostAsync("/api/sessions");
        Assert.That(createRes.Ok, Is.True);
        var id = (await createRes.JsonAsync())?.GetProperty("id").GetString();

        // Verify in list
        var listRes = await _api.GetAsync("/api/sessions");
        var sessions = await listRes.JsonAsync();
        var found = sessions?.EnumerateArray().Any(s => s.GetProperty("id").GetString() == id);
        Assert.That(found, Is.True, "Session should appear in list");

        // Delete
        var deleteRes = await _api.DeleteAsync($"/api/sessions/{id}");
        Assert.That(deleteRes.Ok, Is.True);
    }

    [Test]
    public async Task Integration_AllFiveAgentsAccessible()
    {
        foreach (var agent in AllAgents)
        {
            var root = await _api.GetAsync($"/a2a/{agent}");
            Assert.That(root.Ok, Is.True, $"{agent} root");

            var card = await _api.GetAsync($"/a2a/{agent}/.well-known/agent.json");
            Assert.That(card.Ok, Is.True, $"{agent} card");

            var tasks = await _api.GetAsync($"/a2a/{agent}/a2a/tasks");
            Assert.That(tasks.Ok, Is.True, $"{agent} tasks");

            Console.WriteLine($"{agent}: all endpoints accessible");
        }
    }
}
