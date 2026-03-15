using System.ComponentModel;
using System.Data;
using ModelContextProtocol.Server;

namespace AspireOllama.ApiService.Services.Tools;

/// <summary>
/// MCP Server tools for calculator operations.
/// Exposed via HTTP MCP endpoint at /mcp.
/// </summary>
[McpServerToolType]
public static class McpCalculatorTool
{
    /// <summary>
    /// Evaluates a mathematical expression and returns the result.
    /// </summary>
    [McpServerTool, Description("Evaluates a mathematical expression and returns the result. Supports basic arithmetic (+, -, *, /), parentheses, and modulo (%).")]
    public static string calculator(
        [Description("The mathematical expression to evaluate, e.g., '2 + 2 * 3' or '(10 + 5) / 3'")]
        string expression)
    {
        try
        {
            // Sanitize input - only allow safe characters
            var sanitized = new string(expression.Where(c =>
                char.IsDigit(c) || c == '+' || c == '-' || c == '*' || c == '/' ||
                c == '(' || c == ')' || c == '.' || c == ' ' || c == '%').ToArray());

            if (string.IsNullOrWhiteSpace(sanitized))
            {
                return "Error: Invalid expression. Please use numbers and operators (+, -, *, /, %).";
            }

            var table = new DataTable();
            var result = table.Compute(sanitized, string.Empty);

            return $"{result}";
        }
        catch (SyntaxErrorException ex)
        {
            return $"Error: Invalid expression syntax - {ex.Message}";
        }
        catch (EvaluateException ex)
        {
            return $"Error: Cannot evaluate expression - {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}

/// <summary>
/// MCP Server tools for time operations.
/// </summary>
[McpServerToolType]
public static class McpTimeTool
{
    /// <summary>
    /// Gets the current time in a specified timezone.
    /// </summary>
    [McpServerTool, Description("Gets the current date and time in a specified timezone or city.")]
    public static string get_time(
        [Description("The timezone or city name (e.g., 'UTC', 'Tokyo', 'London', 'New York')")]
        string timezone)
    {
        try
        {
            TimeZoneInfo tz;

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
                    "mumbai" or "india" => TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"),
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
            return $"Unknown timezone: {timezone}. Try 'UTC', 'Tokyo', 'London', 'New York', 'Paris', 'Sydney', or 'Mumbai'.";
        }
    }
}

/// <summary>
/// MCP Server tools for weather information (demo/mock data).
/// </summary>
[McpServerToolType]
public static class McpWeatherTool
{
    /// <summary>
    /// Gets weather information for a city (mock data for demo).
    /// </summary>
    [McpServerTool, Description("Gets current weather information for a specified city. Returns temperature, conditions, and humidity.")]
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

            Note: This is demo data.
            """;
    }
}

/// <summary>
/// MCP Server tools for unit conversions.
/// </summary>
[McpServerToolType]
public static class McpConversionTool
{
    /// <summary>
    /// Converts between common units.
    /// </summary>
    [McpServerTool, Description("Converts values between common units (length, weight, temperature).")]
    public static string convert_units(
        [Description("The numeric value to convert")]
        double value,
        [Description("The source unit (e.g., 'km', 'miles', 'kg', 'lbs', 'celsius', 'fahrenheit')")]
        string from_unit,
        [Description("The target unit (e.g., 'km', 'miles', 'kg', 'lbs', 'celsius', 'fahrenheit')")]
        string to_unit)
    {
        var fromLower = from_unit.ToLowerInvariant();
        var toLower = to_unit.ToLowerInvariant();

        try
        {
            double result = (fromLower, toLower) switch
            {
                // Length
                ("km", "miles") => value * 0.621371,
                ("miles", "km") => value * 1.60934,
                ("m", "ft") or ("meters", "feet") => value * 3.28084,
                ("ft", "m") or ("feet", "meters") => value * 0.3048,
                ("cm", "inches") => value * 0.393701,
                ("inches", "cm") => value * 2.54,

                // Weight
                ("kg", "lbs") or ("kg", "pounds") => value * 2.20462,
                ("lbs", "kg") or ("pounds", "kg") => value * 0.453592,
                ("g", "oz") or ("grams", "ounces") => value * 0.035274,
                ("oz", "g") or ("ounces", "grams") => value * 28.3495,

                // Temperature
                ("celsius", "fahrenheit") or ("c", "f") => (value * 9 / 5) + 32,
                ("fahrenheit", "celsius") or ("f", "c") => (value - 32) * 5 / 9,
                ("celsius", "kelvin") or ("c", "k") => value + 273.15,
                ("kelvin", "celsius") or ("k", "c") => value - 273.15,

                _ => throw new ArgumentException($"Unknown conversion: {from_unit} to {to_unit}")
            };

            return $"{value} {from_unit} = {result:F4} {to_unit}";
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }
    }
}
