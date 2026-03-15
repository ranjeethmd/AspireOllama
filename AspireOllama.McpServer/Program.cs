using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (health checks, OpenTelemetry, etc.)
builder.AddServiceDefaults();

// Add MCP server with HTTP transport
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "AspireOllama MCP Tools Server",
            Version = "1.0.0"
        };
    })
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Map Aspire default endpoints (health checks)
app.MapDefaultEndpoints();

// Map MCP endpoint at /mcp path
app.MapMcp("/mcp");

// Add a simple test endpoint to verify server is running
app.MapGet("/", () => "MCP Server is running. Connect to /mcp for MCP protocol.");

app.Run();

/// <summary>
/// Sample weather tool for MCP demonstration.
/// </summary>
[McpServerToolType]
public static class WeatherTool
{
    /// <summary>
    /// Gets the current weather for a specified city.
    /// This is a demo tool that returns mock weather data.
    /// </summary>
    [McpServerTool, Description("Gets the current weather for a specified city. Returns temperature, conditions, and humidity.")]
    public static string get_weather(
        [Description("The city name to get weather for (e.g., 'London', 'New York', 'Tokyo')")]
        string city)
    {
        // Mock weather data for demonstration
        var random = new Random(city.GetHashCode());
        var temperature = random.Next(-10, 35);
        var conditions = new[] { "Sunny", "Cloudy", "Rainy", "Partly Cloudy", "Stormy", "Snowy" };
        var condition = conditions[Math.Abs(city.GetHashCode()) % conditions.Length];
        var humidity = random.Next(30, 90);

        return $"""
            Weather for {city}:
            Temperature: {temperature}°C ({temperature * 9 / 5 + 32}°F)
            Conditions: {condition}
            Humidity: {humidity}%

            Note: This is demo data from the sample MCP server.
            """;
    }
}

/// <summary>
/// Sample time tool for MCP demonstration.
/// </summary>
[McpServerToolType]
public static class TimeTool
{
    /// <summary>
    /// Gets the current time in a specified timezone.
    /// </summary>
    [McpServerTool, Description("Gets the current time in a specified timezone or city. Returns formatted date and time.")]
    public static string get_time(
        [Description("The timezone or city name (e.g., 'UTC', 'America/New_York', 'Tokyo', 'London')")]
        string timezone)
    {
        try
        {
            TimeZoneInfo tz;

            // Try to find timezone by various methods
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            }
            catch
            {
                // Try common city names
                tz = timezone.ToLowerInvariant() switch
                {
                    "tokyo" => TimeZoneInfo.FindSystemTimeZoneById("Tokyo Standard Time"),
                    "london" => TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time"),
                    "new york" or "newyork" => TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"),
                    "los angeles" or "losangeles" => TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"),
                    "paris" => TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time"),
                    "sydney" => TimeZoneInfo.FindSystemTimeZoneById("AUS Eastern Standard Time"),
                    "utc" or "gmt" => TimeZoneInfo.Utc,
                    _ => throw new TimeZoneNotFoundException()
                };
            }

            var time = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);

            return $"""
                Current time in {tz.DisplayName}:
                Date: {time:dddd, MMMM d, yyyy}
                Time: {time:h:mm:ss tt}
                UTC Offset: {tz.GetUtcOffset(time)}
                """;
        }
        catch (TimeZoneNotFoundException)
        {
            return $"Unknown timezone: {timezone}. Try 'UTC', 'Tokyo', 'London', 'New York', 'Paris', or 'Sydney'.";
        }
    }
}
