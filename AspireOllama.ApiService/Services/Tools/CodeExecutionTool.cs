using System.ComponentModel;
using AspireOllama.Shared;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Options;

namespace AspireOllama.ApiService.Services.Tools;

/// <summary>
/// Tool for executing C# code in a sandboxed environment.
/// Uses Roslyn scripting with restricted options for safety.
/// </summary>
public class CodeExecutionTool : ITool
{
    private readonly ILogger<CodeExecutionTool> _logger;
    private readonly bool _isEnabled;
    private readonly int _timeoutSeconds;

    public string Name => "execute_code";
    public string Description => "Executes C# code in a sandboxed environment";
    public bool IsEnabled => _isEnabled;

    public CodeExecutionTool(IOptions<ToolConfiguration> config, IConfiguration configuration, ILogger<CodeExecutionTool> logger)
    {
        _logger = logger;
        _isEnabled = config.Value.EnableCodeExecution;
        _timeoutSeconds = configuration.GetValue("Tools:CodeExecution:TimeoutSeconds", 5);
    }

    /// <summary>
    /// Executes C# code and returns the result.
    /// </summary>
    /// <param name="code">The C# code to execute.</param>
    /// <returns>The result of the code execution or an error message.</returns>
    [Description("Executes C# code and returns the result. Use for calculations, data transformations, or demonstrations. Code runs in a restricted sandbox with limited namespaces and a 5-second timeout.")]
    public async Task<string> ExecuteAsync(
        [Description("The C# code to execute. Must be a complete expression or statement that can return a value. Example: 'Math.Pow(2, 10)' or 'Enumerable.Range(1, 5).Sum()'")]
        string code,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Code execution requested: {Code}", code);

        if (string.IsNullOrWhiteSpace(code))
        {
            return "Error: Code cannot be empty.";
        }

        // Basic security checks
        var lowerCode = code.ToLowerInvariant();
        var blockedPatterns = new[]
        {
            "system.io", "file.", "directory.", "process.", "environment.",
            "reflection", "assembly", "type.gettype", "activator.", "marshal.",
            "unsafe", "fixed(", "stackalloc", "httpwebrequest", "httpclient",
            "socket", "tcpclient", "udpclient", "webrequest", "webclient"
        };

        foreach (var pattern in blockedPatterns)
        {
            if (lowerCode.Contains(pattern))
            {
                _logger.LogWarning("Blocked code pattern detected: {Pattern}", pattern);
                return $"Error: Code contains blocked pattern '{pattern}'. File, network, reflection, and process operations are not allowed.";
            }
        }

        try
        {
            // Configure restricted script options
            var options = ScriptOptions.Default
                .WithImports("System", "System.Linq", "System.Collections.Generic", "System.Text", "System.Math")
                .WithAllowUnsafe(false)
                .WithCheckOverflow(true)
                .WithOptimizationLevel(Microsoft.CodeAnalysis.OptimizationLevel.Release);

            // Create timeout protection
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

            _logger.LogInformation("Executing code with {Timeout}s timeout", _timeoutSeconds);

            var result = await CSharpScript.EvaluateAsync<object>(code, options, cancellationToken: cts.Token);

            var resultString = result?.ToString() ?? "null";
            _logger.LogInformation("Code execution result: {Result}", resultString);

            return resultString;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Code execution timed out after {Timeout} seconds", _timeoutSeconds);
            return $"Error: Code execution timed out after {_timeoutSeconds} seconds.";
        }
        catch (CompilationErrorException ex)
        {
            var errors = string.Join(Environment.NewLine, ex.Diagnostics.Select(d => d.ToString()));
            _logger.LogWarning(ex, "Code compilation error: {Errors}", errors);
            return $"Compilation error:\n{errors}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Code execution error");
            return $"Execution error: {ex.Message}";
        }
    }
}
