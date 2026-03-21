namespace AspireOllama.Tests.UI;

[SetUpFixture]
public class GlobalSetup
{
    public static bool ServicesAvailable { get; private set; }
    public static string BaseUrl { get; private set; } = "https://localhost:7170";

    [OneTimeSetUp]
    public async Task Setup()
    {
        Console.WriteLine("Installing Playwright browsers...");
        var exitCode = Microsoft.Playwright.Program.Main(["install"]);
        if (exitCode != 0)
            throw new Exception($"Playwright browser installation failed with exit code {exitCode}");
        Console.WriteLine("Playwright browsers installed successfully.");

        // Resolve base URL
        var envUrl = Environment.GetEnvironmentVariable("APP_URL")
                  ?? Environment.GetEnvironmentVariable("GATEWAY_URL");
        if (!string.IsNullOrEmpty(envUrl))
            BaseUrl = envUrl;

        // Check if services are reachable
        ServicesAvailable = await CheckConnectivity(BaseUrl);
        Console.WriteLine(ServicesAvailable
            ? $"Services available at {BaseUrl}"
            : $"Services NOT available at {BaseUrl} — tests will be skipped");
    }

    private static async Task<bool> CheckConnectivity(string url)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync(url);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Shared base for all test fixtures — resolves URL and skips when services are unavailable.
/// </summary>
public abstract class AppTestBase : Microsoft.Playwright.NUnit.PageTest
{
    protected string BaseUrl => GlobalSetup.BaseUrl;

    [SetUp]
    public void SkipIfServicesUnavailable()
    {
        if (!GlobalSetup.ServicesAvailable)
            Assert.Ignore("Services not available — skipping test");
    }
}
