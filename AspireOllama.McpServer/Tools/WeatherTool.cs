using ModelContextProtocol.Server;
using System.ComponentModel;

[McpServerToolType]
public static class WeatherTool
{
    [McpServerTool, Description("Gets the current weather for a specified city. Returns temperature, conditions, and humidity.")]
    public static string get_weather(
        [Description("The city name to get weather for (e.g., 'London', 'New York', 'Tokyo')")]
        string city)
    {
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
            (Demo data)
            """;
    }
}
