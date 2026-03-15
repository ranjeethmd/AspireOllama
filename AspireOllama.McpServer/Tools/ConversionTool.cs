using ModelContextProtocol.Server;
using System.ComponentModel;

[McpServerToolType]
public static class ConversionTool
{
    [McpServerTool, Description("Converts values between common units (length, weight, temperature).")]
    public static string convert_units(
        [Description("The numeric value to convert")]
        double value,
        [Description("The source unit (e.g., 'km', 'miles', 'kg', 'lbs', 'celsius', 'fahrenheit')")]
        string from_unit,
        [Description("The target unit")]
        string to_unit)
    {
        var fromLower = from_unit.ToLowerInvariant();
        var toLower = to_unit.ToLowerInvariant();

        try
        {
            double result = (fromLower, toLower) switch
            {
                ("km", "miles") => value * 0.621371,
                ("miles", "km") => value * 1.60934,
                ("m", "ft") or ("meters", "feet") => value * 3.28084,
                ("ft", "m") or ("feet", "meters") => value * 0.3048,
                ("cm", "inches") => value * 0.393701,
                ("inches", "cm") => value * 2.54,
                ("kg", "lbs") or ("kg", "pounds") => value * 2.20462,
                ("lbs", "kg") or ("pounds", "kg") => value * 0.453592,
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
