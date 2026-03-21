namespace AspireOllama.Tests.UI;

[SetUpFixture]
public class GlobalSetup
{
    [OneTimeSetUp]
    public async Task Setup()
    {
        Console.WriteLine("Installing Playwright browsers...");
        var exitCode = Microsoft.Playwright.Program.Main(["install"]);
        if (exitCode != 0)
            throw new Exception($"Playwright browser installation failed with exit code {exitCode}");
        Console.WriteLine("Playwright browsers installed successfully.");
    }
}

/// <summary>
/// Shared base for all test fixtures — resolves APP_URL / GATEWAY_URL once.
/// </summary>
public abstract class AppTestBase : Microsoft.Playwright.NUnit.PageTest
{
    protected string BaseUrl = "https://localhost:7170";

    [SetUp]
    public void ResolveBaseUrl()
    {
        var envUrl = Environment.GetEnvironmentVariable("APP_URL")
                  ?? Environment.GetEnvironmentVariable("GATEWAY_URL");
        if (!string.IsNullOrEmpty(envUrl))
            BaseUrl = envUrl;
    }
}
