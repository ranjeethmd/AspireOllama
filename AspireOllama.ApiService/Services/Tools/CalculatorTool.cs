using AspireOllama.Shared;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Options;
using System.ComponentModel;

namespace AspireOllama.ApiService.Services.Tools;

public class CalculatorTool : ITool
{
    private readonly ILogger<CalculatorTool> _logger;
    private readonly bool _isEnabled;

    private static readonly ScriptOptions ScriptOpts = ScriptOptions.Default
        .WithImports("System", "System.Math");

    public string Name => "calculator";
    public string Description => "Evaluates mathematical expressions";
    public bool IsEnabled => _isEnabled;

    public CalculatorTool(IOptions<ToolConfiguration> config, ILogger<CalculatorTool> logger)
    {
        _logger = logger;
        _isEnabled = config.Value.EnableCalculator;
    }

    [Description("Evaluate a mathematical expression. Supports +, -, *, /, parentheses, powers (^), and common math functions like sqrt, pow, abs, sin, cos, tan, log, PI. Use this for any calculation the user asks for.")]
    public async Task<string> Calculate(
        [Description("The chat session ID")] string session_id,
        [Description("The mathematical expression to evaluate, e.g. '(15 * 7) + 23' or 'Sqrt(144)' or 'Pow(2,10)'")] string expression)
    {
        _logger.LogInformation("Calculator tool: {Expression}", expression);
        try
        {
            // Normalize common math notations to C# syntax
            var sanitized = expression
                .Replace("^", ",")     // 2^10 → Pow(2,10) handled below
                .Replace("sqrt", "Sqrt")
                .Replace("pow", "Pow")
                .Replace("abs", "Abs")
                .Replace("sin", "Sin")
                .Replace("cos", "Cos")
                .Replace("tan", "Tan")
                .Replace("log", "Log10")
                .Replace("ln", "Log")
                .Replace("pi", "PI");

            // Handle ^ operator: "2^10" was split to "2,10" — wrap in Pow()
            // Only if original had ^ and result doesn't already have Pow
            if (expression.Contains('^') && !expression.Contains("pow", StringComparison.OrdinalIgnoreCase))
            {
                // Re-sanitize: use original with Pow wrapper
                sanitized = System.Text.RegularExpressions.Regex.Replace(
                    expression, @"(\d+\.?\d*)\s*\^\s*(\d+\.?\d*)", "Pow($1,$2)");
            }

            // Suffix integer literals with 'd' to force double arithmetic (avoids int overflow)
            sanitized = System.Text.RegularExpressions.Regex.Replace(
                sanitized, @"(?<!\w)(\d+)(?![\d\.\w])", "$1d");

            var result = await CSharpScript.EvaluateAsync<double>(sanitized, ScriptOpts);

            return $"Result: {result}";
        }
        catch (Exception ex)
        {
            return $"Error evaluating '{expression}': {ex.Message}";
        }
    }
}
