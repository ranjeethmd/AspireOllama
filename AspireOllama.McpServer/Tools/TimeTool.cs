
using ModelContextProtocol.Server;
using System.ComponentModel;

[McpServerToolType]
public static class TimeTool
{
    [McpServerTool, Description("Gets the current time in a specified timezone or city.")]
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