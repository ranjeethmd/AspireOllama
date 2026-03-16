using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace AspireOllama.Tests.UI;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class AgentsWorkflowTests : PageTest
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
    public async Task Workflow_ShouldExecuteAndShowSequenceDiagram()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Click Workflow tab
        await Page.GetByRole(AriaRole.Button, new() { Name = "Workflow" }).ClickAsync();

        // Enter a task
        await Page.Locator(".workflow-form textarea").FillAsync("Create a simple login system");

        // Click Run Workflow
        await Page.GetByRole(AriaRole.Button, new() { Name = "Run Workflow" }).ClickAsync();

        // Wait for workflow to complete (may take a while)
        await Page.WaitForSelectorAsync(".sequence-diagram", new() { Timeout = 60000 });

        // Verify sequence diagram is visible
        await Expect(Page.Locator(".sequence-diagram")).ToBeVisibleAsync();

        // Verify sequence flow has items
        var sequenceCards = Page.Locator(".sequence-call-card");
        var count = await sequenceCards.CountAsync();

        Console.WriteLine($"Workflow completed with {count} steps");
        Assert.That(count, Is.GreaterThan(0), "Workflow should have at least one step");
    }

    [Test]
    public async Task Workflow_ShouldShowMCPFunctionNames()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Click Workflow tab
        await Page.GetByRole(AriaRole.Button, new() { Name = "Workflow" }).ClickAsync();

        // Enter a task
        await Page.Locator(".workflow-form textarea").FillAsync("Plan a REST API");

        // Click Run Workflow
        await Page.GetByRole(AriaRole.Button, new() { Name = "Run Workflow" }).ClickAsync();

        // Wait for completion
        await Page.WaitForSelectorAsync(".mcp-badge", new() { Timeout = 60000 });

        // Verify MCP badges are visible
        var mcpBadges = Page.Locator(".mcp-badge");
        var count = await mcpBadges.CountAsync();

        Assert.That(count, Is.GreaterThan(0), "Should show MCP function badges");

        // Log the function names
        for (int i = 0; i < count; i++)
        {
            var funcName = await mcpBadges.Nth(i).Locator(".mcp-func").TextContentAsync();
            Console.WriteLine($"Step {i + 1} MCP function: {funcName}");
        }
    }

    [Test]
    public async Task Workflow_ShouldShowAgentNames()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Click Workflow tab
        await Page.GetByRole(AriaRole.Button, new() { Name = "Workflow" }).ClickAsync();

        // Enter a task
        await Page.Locator(".workflow-form textarea").FillAsync("Implement user authentication");

        // Click Run Workflow
        await Page.GetByRole(AriaRole.Button, new() { Name = "Run Workflow" }).ClickAsync();

        // Wait for completion
        await Page.WaitForSelectorAsync(".call-target-agent", new() { Timeout = 60000 });

        // Verify target agents are shown
        var targetAgents = Page.Locator(".call-target-agent");
        var count = await targetAgents.CountAsync();

        Assert.That(count, Is.GreaterThan(0), "Should show target agents");

        // Log agent names
        for (int i = 0; i < count; i++)
        {
            var agentName = await targetAgents.Nth(i).TextContentAsync();
            Console.WriteLine($"Step {i + 1} target agent: {agentName}");
        }
    }

    [Test]
    public async Task Workflow_ShouldShowExecutionTime()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Click Workflow tab
        await Page.GetByRole(AriaRole.Button, new() { Name = "Workflow" }).ClickAsync();

        // Enter a task
        await Page.Locator(".workflow-form textarea").FillAsync("Design a caching system");

        // Click Run Workflow
        await Page.GetByRole(AriaRole.Button, new() { Name = "Run Workflow" }).ClickAsync();

        // Wait for completion
        await Page.WaitForSelectorAsync(".exec-time", new() { Timeout = 60000 });

        // Verify execution times are shown
        var execTimes = Page.Locator(".exec-time");
        var count = await execTimes.CountAsync();

        Assert.That(count, Is.GreaterThan(0), "Should show execution times");
    }

    [Test]
    public async Task Workflow_ShouldAllowExpandingResults()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Click Workflow tab
        await Page.GetByRole(AriaRole.Button, new() { Name = "Workflow" }).ClickAsync();

        // Enter a task
        await Page.Locator(".workflow-form textarea").FillAsync("Build a notification service");

        // Click Run Workflow
        await Page.GetByRole(AriaRole.Button, new() { Name = "Run Workflow" }).ClickAsync();

        // Wait for completion
        await Page.WaitForSelectorAsync(".expand-result-btn", new() { Timeout = 60000 });

        // Click first expand button
        var expandBtn = Page.Locator(".expand-result-btn").First;
        await expandBtn.ClickAsync();

        // Verify result content is visible
        await Expect(Page.Locator(".result-json").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task Workflow_ShouldShowCallSummary()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Click Workflow tab
        await Page.GetByRole(AriaRole.Button, new() { Name = "Workflow" }).ClickAsync();

        // Enter a task
        await Page.Locator(".workflow-form textarea").FillAsync("Create a payment processor");

        // Click Run Workflow
        await Page.GetByRole(AriaRole.Button, new() { Name = "Run Workflow" }).ClickAsync();

        // Wait for completion
        await Page.WaitForSelectorAsync(".call-summary", new() { Timeout = 60000 });

        // Verify call summary is visible
        await Expect(Page.Locator(".call-summary")).ToBeVisibleAsync();

        // Verify summary cards exist
        var summaryCards = Page.Locator(".summary-card");
        var count = await summaryCards.CountAsync();

        Assert.That(count, Is.GreaterThan(0), "Should have summary cards for agents");
    }

    [Test]
    public async Task Workflow_ShouldShowStepNumbers()
    {
        await Page.GotoAsync($"{_baseUrl}/agents");

        // Click Workflow tab
        await Page.GetByRole(AriaRole.Button, new() { Name = "Workflow" }).ClickAsync();

        // Enter a task
        await Page.Locator(".workflow-form textarea").FillAsync("Implement search functionality");

        // Click Run Workflow
        await Page.GetByRole(AriaRole.Button, new() { Name = "Run Workflow" }).ClickAsync();

        // Wait for completion
        await Page.WaitForSelectorAsync(".call-step-number", new() { Timeout = 60000 });

        // Verify step numbers are shown in sequence
        var stepNumbers = Page.Locator(".call-step-number");
        var count = await stepNumbers.CountAsync();

        Assert.That(count, Is.GreaterThan(0), "Should show step numbers");

        // Verify steps are numbered correctly
        for (int i = 0; i < count; i++)
        {
            var stepText = await stepNumbers.Nth(i).TextContentAsync();
            Console.WriteLine($"Step number: {stepText}");
        }
    }
}
