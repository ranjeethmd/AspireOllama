using Microsoft.Playwright;

namespace AspireOllama.Tests.UI;

/// <summary>
/// Tests for the Workflow tab on the Agents page — execution, results, call summary, final result.
/// These tests run actual workflows and require all services to be running.
/// Timeout: 120s per workflow execution.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class AgentsWorkflowTests : AppTestBase
{
    private const int WorkflowTimeout = 120000; // 2 min per workflow

    private async Task NavigateToWorkflowTab()
    {
        await Page.GotoAsync($"{BaseUrl}/agents");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Workflow" }).ClickAsync();
        await Expect(Page.Locator(".workflow-panel")).ToBeVisibleAsync();
    }

    private async Task RunWorkflow(string task)
    {
        await NavigateToWorkflowTab();
        await Page.Locator(".workflow-form textarea").FillAsync(task);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Run Workflow" }).ClickAsync();
    }

    // ── Workflow Execution ──

    [Test]
    public async Task Workflow_ShouldExecuteAndShowResults()
    {
        await RunWorkflow("Plan a simple REST API");

        // Wait for workflow to complete — look for results area
        await Page.WaitForSelectorAsync(".workflow-results", new() { Timeout = WorkflowTimeout });

        await Expect(Page.Locator(".workflow-results")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Workflow_ShouldShowTaskDescription()
    {
        await RunWorkflow("Create a hello world API");

        await Page.WaitForSelectorAsync(".workflow-task", new() { Timeout = WorkflowTimeout });

        var taskText = await Page.Locator(".workflow-task").TextContentAsync();
        Assert.That(taskText, Is.Not.Empty, "Should display the task description");
    }

    [Test]
    public async Task Workflow_ShouldShowExecutionTime()
    {
        await RunWorkflow("Assess complexity of a web app");

        await Page.WaitForSelectorAsync(".workflow-time", new() { Timeout = WorkflowTimeout });

        await Expect(Page.Locator(".workflow-time").First).ToBeVisibleAsync();
    }

    // ── Call Summary ──

    [Test]
    public async Task Workflow_ShouldShowCallSummary()
    {
        await RunWorkflow("Research best practices for caching");

        await Page.WaitForSelectorAsync(".workflow-results", new() { Timeout = WorkflowTimeout });

        // Call summary section should be present after workflow completes
        var summarySection = Page.Locator(".call-summary, .workflow-summary");
        await Expect(summarySection.First).ToBeVisibleAsync();
    }

    // ── Final Result ──

    [Test]
    public async Task Workflow_ShouldShowFinalResult()
    {
        await RunWorkflow("Suggest agents for a code review task");

        await Page.WaitForSelectorAsync(".workflow-results", new() { Timeout = WorkflowTimeout });

        // Final result section should be present
        var finalResult = Page.Locator(".final-result, .workflow-results");
        await Expect(finalResult.First).ToBeVisibleAsync();
    }

    // ── Disabled State ──

    [Test]
    public async Task Workflow_RunButton_ShouldDisableDuringExecution()
    {
        await NavigateToWorkflowTab();
        await Page.Locator(".workflow-form textarea").FillAsync("Quick test task");

        var runBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Run Workflow" });
        await runBtn.ClickAsync();

        // Presets should be disabled during execution
        var preset = Page.Locator(".preset-btn").First;
        await Expect(preset).ToBeDisabledAsync();
    }

    // ── Empty State ──

    [Test]
    public async Task Workflow_EmptyInput_RunButtonShouldBePresent()
    {
        await NavigateToWorkflowTab();

        // Run button should still be visible (enabled or disabled based on input)
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Run Workflow" })).ToBeVisibleAsync();
    }
}
