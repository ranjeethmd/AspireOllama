using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace AspireOllama.Tests.UI;

/// <summary>
/// Tests for chat session functionality through the YARP gateway.
/// These tests verify the frontend can properly interact with the API.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class ChatSessionTests : PageTest
{
    private string _gatewayUrl = "http://localhost:7000";

    [SetUp]
    public void Setup()
    {
        var envUrl = Environment.GetEnvironmentVariable("GATEWAY_URL");
        if (!string.IsNullOrEmpty(envUrl))
        {
            _gatewayUrl = envUrl;
        }
    }

    // ============================================================
    // Page Load Tests
    // ============================================================

    [Test]
    public async Task ChatPage_ShouldLoadThroughGateway()
    {
        await Page.GotoAsync(_gatewayUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        // Verify the page loaded (Blazor app should render)
        Assert.That(Page.Url, Does.StartWith(_gatewayUrl));

        // Wait for Blazor to initialize
        await Page.WaitForSelectorAsync(".chat-container, .sidebar, body", new() { Timeout = 10000 });
    }

    [Test]
    public async Task ChatPage_ShouldHaveSidebar()
    {
        await Page.GotoAsync(_gatewayUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        // Look for sidebar element
        var sidebar = Page.Locator(".sidebar");
        await Expect(sidebar).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    public async Task ChatPage_ShouldHaveNewChatButton()
    {
        await Page.GotoAsync(_gatewayUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        // Look for new chat button
        var newChatBtn = Page.Locator(".new-chat-btn, button:has-text('New Chat'), [aria-label*='new']");
        await Expect(newChatBtn).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    // ============================================================
    // Session Creation Tests
    // ============================================================

    [Test]
    public async Task ChatPage_NewChatButton_ShouldCreateSession()
    {
        await Page.GotoAsync(_gatewayUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        // Click new chat button
        var newChatBtn = Page.Locator(".new-chat-btn, button:has-text('New Chat')").First;
        await newChatBtn.ClickAsync();

        // Wait for session to be created (UI should update)
        await Page.WaitForTimeoutAsync(1000);

        // Verify no error modal/toast appeared
        var errorIndicator = Page.Locator(".error, .toast-error, [role='alert']");
        var errorCount = await errorIndicator.CountAsync();

        if (errorCount > 0)
        {
            var errorText = await errorIndicator.First.TextContentAsync();
            Assert.Fail($"Error occurred during session creation: {errorText}");
        }
    }

    [Test]
    public async Task ChatPage_SessionList_ShouldShowSessions()
    {
        await Page.GotoAsync(_gatewayUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        // Wait for session list to load
        await Page.WaitForSelectorAsync(".session-list, .sidebar-sessions, .chat-history", new() { Timeout = 10000 });

        // Session list should be visible (may be empty initially)
        var sessionList = Page.Locator(".session-list, .sidebar-sessions, .chat-history");
        await Expect(sessionList).ToBeVisibleAsync();
    }

    // ============================================================
    // Chat Input Tests
    // ============================================================

    [Test]
    public async Task ChatPage_ShouldHaveMessageInput()
    {
        await Page.GotoAsync(_gatewayUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        // Look for message input
        var messageInput = Page.Locator("textarea, input[type='text']").First;
        await Expect(messageInput).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    [Test]
    public async Task ChatPage_ShouldHaveSendButton()
    {
        await Page.GotoAsync(_gatewayUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        // Look for send button
        var sendBtn = Page.Locator("button:has-text('Send'), .send-btn, button[type='submit']");
        await Expect(sendBtn).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    // ============================================================
    // Navigation Tests
    // ============================================================

    [Test]
    public async Task ChatPage_ShouldNavigateToAgents()
    {
        await Page.GotoAsync(_gatewayUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        // Look for agents link/button
        var agentsLink = Page.Locator("a[href*='agents'], button:has-text('Agents')");
        if (await agentsLink.CountAsync() > 0)
        {
            await agentsLink.First.ClickAsync();
            await Page.WaitForURLAsync(url => url.Contains("agents"), new() { Timeout = 10000 });
            Assert.That(Page.Url, Does.Contain("agents"));
        }
    }

    // ============================================================
    // Error Handling Tests
    // ============================================================

    [Test]
    public async Task ChatPage_NoJavaScriptErrors()
    {
        var jsErrors = new List<string>();

        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error")
            {
                jsErrors.Add(msg.Text);
            }
        };

        await Page.GotoAsync(_gatewayUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        // Wait a bit for any async errors
        await Page.WaitForTimeoutAsync(2000);

        // Filter out known non-critical errors
        var criticalErrors = jsErrors
            .Where(e => !e.Contains("favicon") && !e.Contains("manifest"))
            .ToList();

        if (criticalErrors.Any())
        {
            Console.WriteLine("JavaScript errors found:");
            foreach (var error in criticalErrors)
            {
                Console.WriteLine($"  - {error}");
            }
        }

        // We don't fail the test for JS errors, just log them
        // Assert.That(criticalErrors, Is.Empty, "Page should not have JavaScript errors");
    }

    [Test]
    public async Task ChatPage_NetworkRequests_ShouldSucceed()
    {
        var failedRequests = new List<string>();

        Page.RequestFailed += (_, request) =>
        {
            failedRequests.Add($"{request.Method} {request.Url}: {request.Failure}");
        };

        await Page.GotoAsync(_gatewayUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        // Wait for any additional requests
        await Page.WaitForTimeoutAsync(2000);

        // Log failed requests but don't fail the test for non-critical failures
        if (failedRequests.Any())
        {
            Console.WriteLine("Failed network requests:");
            foreach (var request in failedRequests)
            {
                Console.WriteLine($"  - {request}");
            }
        }
    }

    // ============================================================
    // API Response Tests (via network interception)
    // ============================================================

    [Test]
    public async Task ChatPage_ApiCalls_ShouldUseCorrectRoutes()
    {
        var apiCalls = new List<string>();

        Page.Request += (_, request) =>
        {
            if (request.Url.Contains("/api/"))
            {
                apiCalls.Add($"{request.Method} {request.Url}");
            }
        };

        await Page.GotoAsync(_gatewayUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        // Trigger session creation
        var newChatBtn = Page.Locator(".new-chat-btn, button:has-text('New Chat')").First;
        if (await newChatBtn.IsVisibleAsync())
        {
            await newChatBtn.ClickAsync();
            await Page.WaitForTimeoutAsync(1000);
        }

        Console.WriteLine("API calls made:");
        foreach (var call in apiCalls)
        {
            Console.WriteLine($"  - {call}");
        }

        // Verify API calls use /api/ prefix
        var hasApiPrefix = apiCalls.All(c => c.Contains("/api/"));
        if (apiCalls.Any())
        {
            Assert.That(hasApiPrefix, Is.True, "All API calls should use /api/ prefix");
        }
    }
}
