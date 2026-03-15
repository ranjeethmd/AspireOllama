using System.ComponentModel;
using System.Data;
using AspireOllama.Shared;
using Microsoft.Extensions.Options;

namespace AspireOllama.ApiService.Services.Tools;

/// <summary>
/// Tool for evaluating mathematical expressions.
/// Uses DataTable.Compute for safe expression evaluation.
/// </summary>
public class CalculatorTool : ITool
{
    private readonly ILogger<CalculatorTool> _logger;
    private readonly bool _isEnabled;

    public string Name => "calculator";
    public string Description => "Evaluates mathematical expressions and returns the result";
    public bool IsEnabled => _isEnabled;

    public CalculatorTool(IOptions<ToolConfiguration> config, ILogger<CalculatorTool> logger)
    {
        _logger = logger;
        _isEnabled = config.Value.EnableCalculator;
    }

    /// <summary>
    /// Evaluates a mathematical expression and returns the result.
    /// </summary>
    /// <param name="expression">The mathematical expression to evaluate (e.g., "2 + 2 * 3", "(10 + 5) / 3").</param>
    /// <returns>The result of the calculation or an error message.</returns>
    [Description("Evaluates a mathematical expression and returns the result. Supports basic arithmetic (+, -, *, /), parentheses, and modulo (%). Use this tool when you need to perform calculations.")]
    public string Calculate(
        [Description("The mathematical expression to evaluate, e.g., '2 + 2 * 3' or '(10 + 5) / 3'")]
        string expression)
    {
        _logger.LogInformation("Calculator evaluating expression: {Expression}", expression);

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

            _logger.LogInformation("Calculator result: {Result}", result);
            return $"{result}";
        }
        catch (SyntaxErrorException ex)
        {
            _logger.LogWarning(ex, "Syntax error in expression: {Expression}", expression);
            return $"Error: Invalid expression syntax - {ex.Message}";
        }
        catch (EvaluateException ex)
        {
            _logger.LogWarning(ex, "Evaluation error for expression: {Expression}", expression);
            return $"Error: Cannot evaluate expression - {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error evaluating expression: {Expression}", expression);
            return $"Error: {ex.Message}";
        }
    }
}
