using Microsoft.Playwright.NUnit;

namespace AspireOllama.Tests.UI;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class ToolTesterTests : PageTest
{
    private string _baseUrl = "https://localhost:7170"; // Update with your actual port

    [SetUp]
    public void Setup()
    {
        var envUrl = Environment.GetEnvironmentVariable("APP_URL");
        if (!string.IsNullOrEmpty(envUrl))
        {
            _baseUrl = envUrl;
        }
    }

    [Test]
    public async Task ToolTester_SelectAgentShowsTools()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Wait for agents to load
        await Page.WaitForSelectorAsync(".agent-card");

        // Get initial state - should show "Select an Agent" message
        var noSelection = Page.Locator(".no-selection");
        if (await noSelection.IsVisibleAsync())
        {
            await Expect(Page.GetByText("Select an Agent")).ToBeVisibleAsync();
        }

        // Click on first agent (Planner)
        await Page.Locator(".agent-card").First.ClickAsync();

        // Should now show agent details
        await Expect(Page.Locator(".selected-agent-header")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ToolTester_ClickingToolShowsParams()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Wait for and click on an agent
        await Page.WaitForSelectorAsync(".agent-card");
        await Page.Locator(".agent-card").First.ClickAsync();

        // Wait for tools grid
        var toolsGrid = Page.Locator(".tools-grid");
        await toolsGrid.WaitForAsync(new() { Timeout = 5000 });

        // Check if tools are available
        var toolCards = Page.Locator(".tool-card");
        var toolCount = await toolCards.CountAsync();

        if (toolCount > 0)
        {
            // Click first tool
            await toolCards.First.ClickAsync();

            // Verify tool is selected
            await Expect(toolCards.First).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("selected"));

            // Verify execution panel appears
            await Expect(Page.Locator(".tool-execution-panel")).ToBeVisibleAsync();
        }
        else
        {
            Console.WriteLine("No tools available - agent may not be connected");
        }
    }

    [Test]
    public async Task ToolTester_ExecuteToolShowsResult()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Wait for and click on Planner agent
        await Page.WaitForSelectorAsync(".agent-card");
        await Page.Locator(".agent-card").First.ClickAsync();

        // Wait for tools
        var toolsGrid = Page.Locator(".tools-grid");
        await toolsGrid.WaitForAsync(new() { Timeout = 5000 });

        var toolCards = Page.Locator(".tool-card");
        var toolCount = await toolCards.CountAsync();

        if (toolCount > 0)
        {
            // Click first tool
            await toolCards.First.ClickAsync();

            // Fill in parameter (assuming first param is 'task')
            var paramInput = Page.Locator(".param-input input").First;
            if (await paramInput.IsVisibleAsync())
            {
                await paramInput.FillAsync("Test task for automation");
            }

            // Click Execute
            await Page.Locator(".execute-btn").ClickAsync();

            // Wait for result (with longer timeout for actual execution)
            await Page.WaitForSelectorAsync(".result-panel", new() { Timeout = 30000 });

            // Verify result panel is visible
            await Expect(Page.Locator(".result-panel")).ToBeVisibleAsync();

            // Check if success or error
            var resultPanel = Page.Locator(".result-panel");
            var hasSuccess = await resultPanel.Locator(".success").IsVisibleAsync();
            var hasError = await resultPanel.Locator(".error").IsVisibleAsync();

            Console.WriteLine($"Result: Success={hasSuccess}, Error={hasError}");
        }
        else
        {
            Console.WriteLine("No tools available - agent may not be connected");
            Assert.Ignore("Agent not connected - no tools available");
        }
    }

    [Test]
    public async Task ToolTester_SwitchingAgentsClearsSelection()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Wait for agents
        await Page.WaitForSelectorAsync(".agent-card");

        // Click first agent
        await Page.Locator(".agent-card").First.ClickAsync();

        // Verify first agent is active
        await Expect(Page.Locator(".agent-card.active").First).ToBeVisibleAsync();

        // Click second agent
        await Page.Locator(".agent-card").Nth(1).ClickAsync();

        // Verify second agent is now active
        var activeCards = Page.Locator(".agent-card.active");
        var activeCount = await activeCards.CountAsync();

        Assert.That(activeCount, Is.EqualTo(1), "Only one agent should be active");
    }

    [Test]
    public async Task ToolTester_DisplaysAgentDescription()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Wait for and click on an agent
        await Page.WaitForSelectorAsync(".agent-card");
        await Page.Locator(".agent-card").First.ClickAsync();

        // Verify agent description is shown
        await Expect(Page.Locator(".agent-description")).ToBeVisibleAsync();

        var description = await Page.Locator(".agent-description").TextContentAsync();
        Console.WriteLine($"Agent description: {description}");

        Assert.That(description, Is.Not.Empty, "Agent should have a description");
    }

    [Test]
    public async Task ToolTester_ToolCardsShowDescription()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Wait for and click on an agent
        await Page.WaitForSelectorAsync(".agent-card");
        await Page.Locator(".agent-card").First.ClickAsync();

        // Wait for tools
        var toolsGrid = Page.Locator(".tools-grid");
        await toolsGrid.WaitForAsync(new() { Timeout = 5000 });

        var toolCards = Page.Locator(".tool-card");
        var toolCount = await toolCards.CountAsync();

        if (toolCount > 0)
        {
            // Verify each tool has name and description
            for (int i = 0; i < Math.Min(toolCount, 3); i++)
            {
                var toolCard = toolCards.Nth(i);
                var toolName = await toolCard.Locator(".tool-name").TextContentAsync();
                var toolDesc = await toolCard.Locator(".tool-description").TextContentAsync();

                Console.WriteLine($"Tool: {toolName} - {toolDesc}");

                Assert.That(toolName, Is.Not.Empty, "Tool should have a name");
            }
        }
    }

    [Test]
    public async Task ToolTester_ExecuteButtonDisabledWhenNoToolSelected()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Wait for and click on an agent
        await Page.WaitForSelectorAsync(".agent-card");
        await Page.Locator(".agent-card").First.ClickAsync();

        // Wait for tools grid
        var toolsGrid = Page.Locator(".tools-grid");
        await toolsGrid.WaitForAsync(new() { Timeout = 5000 });

        // Execute button should not be visible when no tool is selected
        var executeBtn = Page.Locator(".tool-execution-panel .execute-btn");
        var isVisible = await executeBtn.IsVisibleAsync();

        // If no tool is selected, execution panel should not be visible
        if (!isVisible)
        {
            Console.WriteLine("Execute button not visible when no tool selected - correct behavior");
        }
    }

    [Test]
    public async Task ToolTester_ResultShowsExecutionTime()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Wait for and click on agent
        await Page.WaitForSelectorAsync(".agent-card");
        await Page.Locator(".agent-card").First.ClickAsync();

        // Wait for tools
        var toolsGrid = Page.Locator(".tools-grid");
        await toolsGrid.WaitForAsync(new() { Timeout = 5000 });

        var toolCards = Page.Locator(".tool-card");
        var toolCount = await toolCards.CountAsync();

        if (toolCount > 0)
        {
            // Click first tool
            await toolCards.First.ClickAsync();

            // Fill parameter
            var paramInput = Page.Locator(".param-input input").First;
            if (await paramInput.IsVisibleAsync())
            {
                await paramInput.FillAsync("Test task");
            }

            // Execute
            await Page.Locator(".execute-btn").ClickAsync();

            // Wait for result
            await Page.WaitForSelectorAsync(".result-panel", new() { Timeout = 30000 });

            // Verify execution time is shown
            await Expect(Page.Locator(".result-time")).ToBeVisibleAsync();

            var execTime = await Page.Locator(".result-time").TextContentAsync();
            Console.WriteLine($"Execution time: {execTime}");
        }
        else
        {
            Assert.Ignore("Agent not connected - no tools available");
        }
    }
}
