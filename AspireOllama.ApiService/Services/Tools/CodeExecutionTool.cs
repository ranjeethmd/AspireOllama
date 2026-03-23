using AspireOllama.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Options;
using System.ComponentModel;

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

        // Security: validate via Roslyn syntax tree to prevent bypass via whitespace/comments/concatenation
        var validationError = ValidateCodeSafety(code);
        if (validationError is not null)
        {
            _logger.LogWarning("Blocked code: {Reason}", validationError);
            return $"Error: {validationError}";
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

    private static readonly HashSet<string> BlockedNamespaces = new(StringComparer.OrdinalIgnoreCase)
    {
        "System.IO", "System.Diagnostics", "System.Reflection", "System.Runtime.InteropServices",
        "System.Net", "System.Net.Http", "System.Net.Sockets", "System.Security",
        "System.Runtime.CompilerServices", "System.Threading", "Microsoft.Win32"
    };

    private static readonly HashSet<string> BlockedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "File", "Directory", "Path", "FileInfo", "DirectoryInfo", "FileStream",
        "StreamReader", "StreamWriter", "Process", "ProcessStartInfo",
        "Environment", "Assembly", "Type", "Activator", "Marshal",
        "HttpClient", "HttpWebRequest", "WebClient", "WebRequest",
        "Socket", "TcpClient", "UdpClient", "TcpListener",
        "RegistryKey", "Registry", "AppDomain", "GC"
    };

    private static string? ValidateCodeSafety(string code)
    {
        // Parse as a script (expression/statements) to match Roslyn scripting behavior
        var tree = CSharpSyntaxTree.ParseText(code, CSharpParseOptions.Default.WithKind(SourceCodeKind.Script));
        var root = tree.GetRoot();

        // Check for unsafe/fixed/stackalloc keywords
        foreach (var token in root.DescendantTokens())
        {
            if (token.IsKind(SyntaxKind.UnsafeKeyword))
                return "Unsafe code is not allowed.";
            if (token.IsKind(SyntaxKind.StackAllocKeyword))
                return "Stack allocation is not allowed.";
        }

        // Check all identifiers and qualified names against blocklists
        foreach (var node in root.DescendantNodes())
        {
            var name = node switch
            {
                QualifiedNameSyntax q => q.ToString(),
                MemberAccessExpressionSyntax m => m.ToString(),
                IdentifierNameSyntax id => id.Identifier.Text,
                _ => null
            };

            if (name is null) continue;

            // Check if any blocked namespace is referenced
            foreach (var ns in BlockedNamespaces)
            {
                if (name.Contains(ns, StringComparison.OrdinalIgnoreCase))
                    return $"Access to '{ns}' namespace is not allowed.";
            }

            // Check if any blocked type is used as an identifier
            if (node is IdentifierNameSyntax identNode && BlockedTypes.Contains(identNode.Identifier.Text))
                return $"Access to type '{identNode.Identifier.Text}' is not allowed.";
        }

        return null;
    }
}
