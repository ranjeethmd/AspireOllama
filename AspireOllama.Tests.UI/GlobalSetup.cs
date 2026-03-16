using Microsoft.Playwright;

namespace AspireOllama.Tests.UI;

[SetUpFixture]
public class GlobalSetup
{
    [OneTimeSetUp]
    public async Task Setup()
    {
        // Install Playwright browsers if not already installed
        Console.WriteLine("Installing Playwright browsers...");
        var exitCode = Microsoft.Playwright.Program.Main(new[] { "install" });
        if (exitCode != 0)
        {
            throw new Exception($"Playwright browser installation failed with exit code {exitCode}");
        }
        Console.WriteLine("Playwright browsers installed successfully.");
    }
}
