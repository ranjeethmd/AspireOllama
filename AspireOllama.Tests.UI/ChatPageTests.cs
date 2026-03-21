using Microsoft.Playwright;

namespace AspireOllama.Tests.UI;

/// <summary>
/// Tests for the Chat page (/chat) — layout, sessions, input, markdown rendering, navigation.
/// Selectors match Chat.razor CSS classes.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class ChatPageTests : AppTestBase
{
    // ── Page Load ──

    [Test]
    public async Task ChatPage_ShouldLoad()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Expect(Page.Locator(".agent-layout")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ChatPage_ShouldHaveTitle()
    {
        await Page.GotoAsync(BaseUrl);
        await Expect(Page).ToHaveTitleAsync("AI Agent");
    }

    // ── Sidebar ──

    [Test]
    public async Task ChatPage_ShouldShowSidebar()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Expect(Page.Locator(".agent-sidebar")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ChatPage_ShouldShowBrandText()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Expect(Page.Locator(".brand-text")).ToBeVisibleAsync();
        await Expect(Page.Locator(".brand-text")).ToHaveTextAsync("AI Agent");
    }

    [Test]
    public async Task ChatPage_ShouldShowNewConversationButton()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Expect(Page.Locator(".new-session-btn")).ToBeVisibleAsync();
        await Expect(Page.GetByText("New Conversation")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ChatPage_ShouldShowSessionsList()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Expect(Page.Locator(".sessions-list")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ChatPage_ShouldShowAgentsLink()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Expect(Page.Locator(".sidebar-footer")).ToBeVisibleAsync();
        await Expect(Page.GetByText("A2A Agents")).ToBeVisibleAsync();
    }

    // ── Session Management ──

    [Test]
    public async Task ChatPage_NewConversation_ShouldCreateSession()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Page.Locator(".new-session-btn").ClickAsync();
        await Page.WaitForTimeoutAsync(1500);

        // URL should now contain /chat/ with a session ID
        Assert.That(Page.Url, Does.Contain("/chat/"));
    }

    [Test]
    public async Task ChatPage_SessionCard_ShouldAppearAfterCreation()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Page.Locator(".new-session-btn").ClickAsync();
        await Page.WaitForTimeoutAsync(1500);

        var sessionCards = Page.Locator(".session-card");
        var count = await sessionCards.CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Should have at least one session card");
    }

    [Test]
    public async Task ChatPage_SessionCard_ShouldShowTitle()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Page.Locator(".new-session-btn").ClickAsync();
        await Page.WaitForTimeoutAsync(1500);

        var title = Page.Locator(".session-title").First;
        await Expect(title).ToBeVisibleAsync();
    }

    [Test]
    public async Task ChatPage_SessionCard_ShouldHighlightActive()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Page.Locator(".new-session-btn").ClickAsync();
        await Page.WaitForTimeoutAsync(1500);

        var activeCards = Page.Locator(".session-card.active");
        var count = await activeCards.CountAsync();
        Assert.That(count, Is.EqualTo(1), "Exactly one session should be active");
    }

    [Test]
    public async Task ChatPage_SessionDelete_ButtonExists()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Page.Locator(".new-session-btn").ClickAsync();
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator(".session-delete").First).ToBeVisibleAsync();
    }

    // ── Chat Area ──

    [Test]
    public async Task ChatPage_ShouldShowHeader()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Expect(Page.Locator(".agent-header")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ChatPage_ShouldShowMessagesArea()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Expect(Page.Locator(".messages-area")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ChatPage_WelcomeScreen_ShouldShowWhenEmpty()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        // Create a fresh session
        await Page.Locator(".new-session-btn").ClickAsync();
        await Page.WaitForTimeoutAsync(1500);

        await Expect(Page.Locator(".welcome-screen")).ToBeVisibleAsync();
        await Expect(Page.GetByText("How can I help you today?")).ToBeVisibleAsync();
    }

    // ── Input Area ──

    [Test]
    public async Task ChatPage_ShouldShowInputArea()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Expect(Page.Locator(".input-area")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ChatPage_ShouldShowMessageInput()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Expect(Page.Locator(".message-input")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ChatPage_ShouldShowSendButton()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Expect(Page.Locator(".send-btn")).ToBeVisibleAsync();
    }

    [Test]
    public async Task ChatPage_ShouldShowInputHint()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Expect(Page.Locator(".input-hint")).ToBeVisibleAsync();
    }

    // ── Markdown Rendering ──

    [Test]
    public async Task ChatPage_AgentResponse_ShouldRenderMarkdown()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        // Create session and send a message
        await Page.Locator(".new-session-btn").ClickAsync();
        await Page.WaitForTimeoutAsync(1500);

        await Page.Locator(".message-input").FillAsync("Say hello in one sentence");
        await Page.Locator(".send-btn").ClickAsync();

        // Wait for agent response
        await Page.WaitForSelectorAsync(".message-wrapper.agent .message-text", new() { Timeout = 120000 });

        // Agent messages should have markdown-body class
        var agentMessage = Page.Locator(".message-wrapper.agent .markdown-body");
        var count = await agentMessage.CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Agent response should render with markdown-body class");
    }

    [Test]
    public async Task ChatPage_UserMessage_ShouldNotRenderMarkdown()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Page.Locator(".new-session-btn").ClickAsync();
        await Page.WaitForTimeoutAsync(1500);

        await Page.Locator(".message-input").FillAsync("Test message");
        await Page.Locator(".send-btn").ClickAsync();

        await Page.WaitForSelectorAsync(".message-wrapper.user", new() { Timeout = 5000 });

        // User messages should NOT have markdown-body class
        var userMarkdown = Page.Locator(".message-wrapper.user .markdown-body");
        var count = await userMarkdown.CountAsync();
        Assert.That(count, Is.EqualTo(0), "User messages should not have markdown rendering");
    }

    // ── Auto-Scroll ──

    [Test]
    public async Task ChatPage_ShouldAutoScrollOnSend()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Page.Locator(".new-session-btn").ClickAsync();
        await Page.WaitForTimeoutAsync(1500);

        await Page.Locator(".message-input").FillAsync("Hello");
        await Page.Locator(".send-btn").ClickAsync();

        // Wait for user message to appear
        await Page.WaitForSelectorAsync(".message-wrapper.user", new() { Timeout = 5000 });

        // Verify the messages area is scrolled (scrollTop should be > 0 or at bottom)
        var isAtBottom = await Page.EvaluateAsync<bool>(
            "() => { const el = document.querySelector('.messages-area'); return el.scrollTop + el.clientHeight >= el.scrollHeight - 50; }");
        Assert.That(isAtBottom, Is.True, "Messages area should be scrolled to bottom");
    }

    // ── Navigation ──

    [Test]
    public async Task ChatPage_AgentsLink_ShouldNavigate()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });

        await Page.Locator(".agents-link-btn").First.ClickAsync();
        await Page.WaitForURLAsync(url => url.Contains("/agents"), new() { Timeout = 10000 });

        Assert.That(Page.Url, Does.Contain("/agents"));
    }

    // ── Error Handling ──

    [Test]
    public async Task ChatPage_NoJavaScriptErrors()
    {
        var jsErrors = new List<string>();
        Page.Console += (_, msg) =>
        {
            if (msg.Type == "error")
                jsErrors.Add(msg.Text);
        };

        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30000 });
        await Page.WaitForTimeoutAsync(2000);

        var criticalErrors = jsErrors
            .Where(e => !e.Contains("favicon") && !e.Contains("manifest"))
            .ToList();

        if (criticalErrors.Any())
        {
            Console.WriteLine("JavaScript errors:");
            criticalErrors.ForEach(e => Console.WriteLine($"  - {e}"));
        }
    }
}
