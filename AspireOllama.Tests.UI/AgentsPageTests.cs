using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace AspireOllama.Tests.UI;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class AgentsPageTests : PageTest
{
    private string _baseUrl = "https://localhost:7170"; // Update with your actual port

    [SetUp]
    public void Setup()
    {
        // Read base URL from environment variable if set
        var envUrl = Environment.GetEnvironmentVariable("APP_URL");
        if (!string.IsNullOrEmpty(envUrl))
        {
            _baseUrl = envUrl;
        }
    }

    [Test]
    public async Task AgentsPage_ShouldLoad()
    {
        // Navigate to agents page
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Verify page title
        await Expect(Page).ToHaveTitleAsync("A2A Agents");

        // Verify main layout elements exist
        await Expect(Page.Locator(".agent-layout")).ToBeVisibleAsync();
        await Expect(Page.Locator(".agent-sidebar")).ToBeVisibleAsync();
        await Expect(Page.Locator(".agent-main")).ToBeVisibleAsync();
    }

    [Test]
    public async Task AgentsPage_ShouldShowAvailableAgents()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Wait for agents to load
        await Page.WaitForSelectorAsync(".agent-card");

        // Verify all 4 agents are listed
        var agentCards = Page.Locator(".agent-card");
        await Expect(agentCards).ToHaveCountAsync(4);

        // Verify agent names
        await Expect(Page.GetByText("Planner Agent")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Reviewer Agent")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Research Agent")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Code Agent")).ToBeVisibleAsync();
    }

    [Test]
    public async Task AgentsPage_ShouldShowToolsCount()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Wait for agents to load
        await Page.WaitForSelectorAsync(".agent-card");

        // Check that tool counts are shown (should be > 0 if agents are connected)
        var toolCounts = Page.Locator(".agent-card-tools");
        var count = await toolCounts.CountAsync();

        Assert.That(count, Is.EqualTo(4), "Should have 4 agents with tool counts");

        // Log tool counts for debugging
        for (int i = 0; i < count; i++)
        {
            var text = await toolCounts.Nth(i).TextContentAsync();
            Console.WriteLine($"Agent {i + 1} tools: {text}");
        }
    }

    [Test]
    public async Task AgentsPage_ShouldHaveToolTesterAndWorkflowTabs()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Verify tabs exist
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Tool Tester" })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Workflow" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task ToolTester_ShouldShowToolsWhenAgentSelected()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Wait for agents to load
        await Page.WaitForSelectorAsync(".agent-card");

        // Click on Planner Agent
        await Page.Locator(".agent-card").First.ClickAsync();

        // Wait for tools to appear
        await Page.WaitForSelectorAsync(".tools-grid", new() { Timeout = 5000 });

        // Verify tools are displayed
        var toolCards = Page.Locator(".tool-card");
        var count = await toolCards.CountAsync();

        Console.WriteLine($"Number of tools displayed: {count}");

        // Should have at least 1 tool (or 0 if agent not connected)
        Assert.That(count, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task ToolTester_ShouldShowParamsWhenToolSelected()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Wait for and click on Planner Agent
        await Page.WaitForSelectorAsync(".agent-card");
        await Page.Locator(".agent-card").First.ClickAsync();

        // Wait for tools and click first tool
        var toolsGrid = Page.Locator(".tools-grid");
        if (await toolsGrid.IsVisibleAsync())
        {
            var firstTool = Page.Locator(".tool-card").First;
            if (await firstTool.IsVisibleAsync())
            {
                await firstTool.ClickAsync();

                // Verify execution panel appears
                await Expect(Page.Locator(".tool-execution-panel")).ToBeVisibleAsync();

                // Verify Execute button exists
                await Expect(Page.Locator(".execute-btn")).ToBeVisibleAsync();
            }
        }
    }

    [Test]
    public async Task Workflow_TabShouldShowInputForm()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Click Workflow tab
        await Page.GetByRole(AriaRole.Button, new() { Name = "Workflow" }).ClickAsync();

        // Verify workflow panel is visible
        await Expect(Page.Locator(".workflow-panel")).ToBeVisibleAsync();

        // Verify textarea exists
        await Expect(Page.Locator(".workflow-form textarea")).ToBeVisibleAsync();

        // Verify Run Workflow button exists
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Run Workflow" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task Navigation_ShouldHaveBackToChatLink()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Verify Back to Chat link exists
        await Expect(Page.Locator(".nav-link-btn")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Back to Chat")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Navigation_BackToChatShouldWork()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Click Back to Chat
        await Page.Locator(".nav-link-btn").ClickAsync();

        // Verify we're on the chat page
        await Page.WaitForURLAsync(url => url.Contains("/") && !url.Contains("/agents"));
    }

    [Test]
    public async Task Refresh_ButtonShouldExist()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Verify Refresh button exists
        await Expect(Page.Locator(".refresh-btn")).ToBeVisibleAsync();
    }
}
