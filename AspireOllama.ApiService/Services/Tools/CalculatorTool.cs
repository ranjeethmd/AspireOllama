using AspireOllama.Shared;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.Data;

namespace AspireOllama.ApiService.Services.Tools;

public class CalculatorTool : ITool
{
    private readonly ILogger<CalculatorTool> _logger;
    private readonly bool _isEnabled;

    public string Name => "calculator";
    public string Description => "Evaluates mathematical expressions";
    public bool IsEnabled => _isEnabled;

    public CalculatorTool(IOptions<ToolConfiguration> config, ILogger<CalculatorTool> logger)
    {
        _logger = logger;
        _isEnabled = config.Value.EnableCalculator;
    }

    [Description("Evaluate a mathematical expression. Supports +, -, *, /, parentheses, powers (^), and common math functions. Use this for any calculation the user asks for.")]
    public string Calculate(
        [Description("The chat session ID")] string session_id,
        [Description("The mathematical expression to evaluate, e.g. '(15 * 7) + 23' or 'sqrt(144)' or '2^10'")] string expression)
    {
        _logger.LogInformation("Calculator tool: {Expression}", expression);
        try
        {
            var sanitized = expression
                .Replace("^", "**")
                .Replace("sqrt", "Math.Sqrt")
                .Replace("pow", "Math.Pow")
                .Replace("abs", "Math.Abs")
                .Replace("sin", "Math.Sin")
                .Replace("cos", "Math.Cos")
                .Replace("tan", "Math.Tan")
                .Replace("log", "Math.Log10")
                .Replace("ln", "Math.Log")
                .Replace("pi", "Math.PI")
                .Replace("PI", "Math.PI");

            var dt = new DataTable();
            var dtExpression = sanitized.Replace("**", "^");
            if (!sanitized.Contains("Math."))
            {
                var result = dt.Compute(expression, null);
                return $"Result: {result}";
            }

            return $"Expression: {expression} (complex math functions require manual evaluation)";
        }
        catch (Exception ex)
        {
            return $"Error evaluating '{expression}': {ex.Message}";
        }
    }
}
