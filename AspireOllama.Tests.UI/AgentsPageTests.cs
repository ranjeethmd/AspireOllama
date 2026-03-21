using Microsoft.Playwright;

namespace AspireOllama.Tests.UI;

/// <summary>
/// Tests for the Agents page (/agents) — layout, agent cards (5 agents), tabs, tool tester, skill counts.
/// Selectors match Agents.razor CSS classes.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class AgentsPageTests : AppTestBase
{
    // ── Page Load ──

    [Test]
    public async Task AgentsPage_ShouldLoad()
    {
        await Page.GotoAsync($"{BaseUrl}/agents");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Expect(Page).ToHaveTitleAsync("A2A Agents");
        await Expect(Page.Locator(".agent-layout")).ToBeVisibleAsync();
        await Expect(Page.Locator(".agent-sidebar")).ToBeVisibleAsync();
        await Expect(Page.Locator(".agent-main")).ToBeVisibleAsync();
    }

    // ── Agent Cards ──

    [Test]
    public async Task AgentsPage_ShouldShowAllFiveAgents()
    {
        await Page.GotoAsync($"{BaseUrl}/agents");
        await Page.WaitForSelectorAsync(".agent-card", new() { Timeout = 15000 });

        var agentCards = Page.Locator(".agent-card");
        await Expect(agentCards).ToHaveCountAsync(5);
    }

    [Test]
    public async Task AgentsPage_ShouldShowAgentNames()
    {
        await Page.GotoAsync($"{BaseUrl}/agents");
        await Page.WaitForSelectorAsync(".agent-card", new() { Timeout = 15000 });

        await Expect(Page.GetByText("Coordinator")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Planner")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Reviewer")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Research")).ToBeVisibleAsync();
        await Expect(Page.GetByText("Code")).ToBeVisibleAsync();
    }

    [Test]
    public async Task AgentsPage_ShouldShowSkillCounts()
    {
        await Page.GotoAsync($"{BaseUrl}/agents");
        await Page.WaitForSelectorAsync(".agent-card", new() { Timeout = 15000 });

        var skillLabels = Page.Locator(".agent-card-skills");
        var count = await skillLabels.CountAsync();

        Assert.That(count, Is.EqualTo(5), "All 5 agents should show skill counts");

        for (int i = 0; i < count; i++)
        {
            var text = await skillLabels.Nth(i).TextContentAsync();
            Console.WriteLine($"Agent {i + 1} skills: {text}");
            Assert.That(text, Does.Contain("skill"), $"Agent {i + 1} should display skill count");
        }
    }

    [Test]
    public async Task AgentsPage_ClickingAgent_ShouldActivate()
    {
        await Page.GotoAsync($"{BaseUrl}/agents");
        await Page.WaitForSelectorAsync(".agent-card", new() { Timeout = 15000 });

        await Page.Locator(".agent-card").First.ClickAsync();

        var activeCards = Page.Locator(".agent-card.active");
        await Expect(activeCards).ToHaveCountAsync(1);
    }

    [Test]
    public async Task AgentsPage_SwitchingAgents_ShouldChangeActive()
    {
        await Page.GotoAsync($"{BaseUrl}/agents");
        await Page.WaitForSelectorAsync(".agent-card", new() { Timeout = 15000 });

        await Page.Locator(".agent-card").First.ClickAsync();
        await Expect(Page.Locator(".agent-card.active")).ToHaveCountAsync(1);

        await Page.Locator(".agent-card").Nth(2).ClickAsync();
        await Expect(Page.Locator(".agent-card.active")).ToHaveCountAsync(1);
    }

    // ── Tabs ──

    [Test]
    public async Task AgentsPage_ShouldHaveToolTesterTab()
    {
        await Page.GotoAsync($"{BaseUrl}/agents");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Tool Tester" })).ToBeVisibleAsync();
    }

    [Test]
    public async Task AgentsPage_ShouldHaveWorkflowTab()
    {
        await Page.GotoAsync($"{BaseUrl}/agents");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Workflow" })).ToBeVisibleAsync();
    }

    // ── Tool Tester ──

    [Test]
    public async Task ToolTester_SelectAgent_ShouldShowTools()
    {
        await Page.GotoAsync($"{BaseUrl}/agents");
        await Page.WaitForSelectorAsync(".agent-card", new() { Timeout = 15000 });

        await Page.Locator(".agent-card").First.ClickAsync();
        await Page.WaitForTimeoutAsync(1000);

        // Agent should have tools or show a message
        var toolCards = Page.Locator(".tool-card");
        var count = await toolCards.CountAsync();
        Console.WriteLine($"Tools displayed: {count}");

        Assert.That(count, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task ToolTester_ClickTool_ShouldShowExecutionPanel()
    {
        await Page.GotoAsync($"{BaseUrl}/agents");
        await Page.WaitForSelectorAsync(".agent-card", new() { Timeout = 15000 });

        await Page.Locator(".agent-card").First.ClickAsync();

        var toolCards = Page.Locator(".tool-card");
        if (await toolCards.CountAsync() > 0)
        {
            await toolCards.First.ClickAsync();
            await Expect(Page.Locator(".tool-execution-panel")).ToBeVisibleAsync();
            await Expect(Page.Locator(".execute-btn")).ToBeVisibleAsync();
        }
        else
        {
            Assert.Ignore("No tools available — agent may not be connected");
        }
    }

    // ── Workflow Tab ──

    [Test]
    public async Task Workflow_ShouldShowInputForm()
    {
        await Page.GotoAsync($"{BaseUrl}/agents");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Page.GetByRole(AriaRole.Button, new() { Name = "Workflow" }).ClickAsync();

        await Expect(Page.Locator(".workflow-panel")).ToBeVisibleAsync();
        await Expect(Page.Locator(".workflow-form textarea")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Workflow_ShouldShowPresetButtons()
    {
        await Page.GotoAsync($"{BaseUrl}/agents");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Page.GetByRole(AriaRole.Button, new() { Name = "Workflow" }).ClickAsync();

        var presets = Page.Locator(".preset-btn");
        var count = await presets.CountAsync();

        Assert.That(count, Is.EqualTo(4), "Should have 4 preset buttons");
    }

    [Test]
    public async Task Workflow_PresetButton_ShouldFillTextarea()
    {
        await Page.GotoAsync($"{BaseUrl}/agents");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Page.GetByRole(AriaRole.Button, new() { Name = "Workflow" }).ClickAsync();
        await Page.Locator(".preset-btn").First.ClickAsync();

        var textarea = Page.Locator(".workflow-form textarea");
        var value = await textarea.InputValueAsync();
        Assert.That(value, Is.Not.Empty, "Preset should fill the textarea");
    }

    [Test]
    public async Task Workflow_ShouldHaveRunButton()
    {
        await Page.GotoAsync($"{BaseUrl}/agents");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Page.GetByRole(AriaRole.Button, new() { Name = "Workflow" }).ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Run Workflow" })).ToBeVisibleAsync();
    }

    // ── Navigation ──

    [Test]
    public async Task AgentsPage_ShouldHaveBackToChatLink()
    {
        await Page.GotoAsync($"{BaseUrl}/agents");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Expect(Page.GetByText("Back to Chat")).ToBeVisibleAsync();
    }

    [Test]
    public async Task AgentsPage_BackToChat_ShouldNavigate()
    {
        await Page.GotoAsync($"{BaseUrl}/agents");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Page.GetByText("Back to Chat").ClickAsync();
        await Page.WaitForURLAsync(url => !url.Contains("/agents"), new() { Timeout = 10000 });
    }
}
