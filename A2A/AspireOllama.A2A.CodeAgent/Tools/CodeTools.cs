using AspireOllama.A2A.CodeAgent.Models.Mcp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using ModelContextProtocol.Server;
using OllamaSharp;
using OllamaSharp.AsyncEnumerableExtensions;
using System.ComponentModel;
using System.Text.Json;

namespace AspireOllama.A2A.CodeAgent.Tools;

[McpServerToolType]
public static class CodeTools
{
    private static IOllamaApiClient? _ollamaClient;

    private static readonly ScriptOptions DefaultScriptOptions = ScriptOptions.Default
        .WithReferences(typeof(object).Assembly, typeof(Enumerable).Assembly)
        .WithImports("System", "System.Linq", "System.Collections.Generic", "System.Text");

    public static void Initialize(IOllamaApiClient client) => _ollamaClient = client;

    [McpServerTool, Description("Executes C# code in a sandboxed environment")]
    public static async Task<ExecutionResult> execute_csharp(
        [Description("The C# code to execute")] string code,
        [Description("Timeout in seconds (max: 30)")] int timeoutSeconds = 5)
    {
        timeoutSeconds = Math.Min(timeoutSeconds, 30);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var result = await CSharpScript.EvaluateAsync<object>(code, DefaultScriptOptions, cancellationToken: cts.Token);
            sw.Stop();
            return new ExecutionResult { Success = true, Output = result?.ToString() ?? "(null)", Type = result?.GetType().Name ?? "null", ExecutionTimeMs = sw.ElapsedMilliseconds };
        }
        catch (CompilationErrorException ex)
        {
            sw.Stop();
            return new ExecutionResult { Success = false, Error = "Compilation error", Details = string.Join("\n", ex.Diagnostics), ExecutionTimeMs = sw.ElapsedMilliseconds };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new ExecutionResult { Success = false, Error = "Timeout", Details = "Exceeded " + timeoutSeconds + "s limit", ExecutionTimeMs = sw.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ExecutionResult { Success = false, Error = ex.GetType().Name, Details = ex.Message, ExecutionTimeMs = sw.ElapsedMilliseconds };
        }
    }

    [McpServerTool, Description("Uses AI to generate code based on requirements")]
    public static async Task<CodeGenResult> generate_code(
        [Description("Description of what the code should do")] string description,
        [Description("Programming language")] string language = "csharp")
    {
        var client = GetClient();
        var prompt = "You are an expert developer. Generate " + language + " code for:\n\n" + description + "\n\n" +
            "Respond in JSON: {\"code\": \"generated code\", \"explanation\": \"what it does\", \"usage\": \"how to use it\"}";

        try
        {
            var result = await client.GenerateAsync(prompt).StreamToEndAsync();
            var content = result?.Response ?? "";
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<AICodeGenResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed is not null)
                {
                    return new CodeGenResult { Success = true, Language = language, Code = parsed.Code ?? "", Description = description, Explanation = parsed.Explanation ?? "", Usage = parsed.Usage ?? "", AiPowered = true };
                }
            }
            return new CodeGenResult { Success = false, Language = language, Description = description, Error = content, AiPowered = false };
        }
        catch (Exception ex)
        {
            return new CodeGenResult { Success = false, Language = language, Description = description, Error = "Error: " + ex.Message, AiPowered = false };
        }
    }

    [McpServerTool, Description("Uses AI to analyze code structure and complexity")]
    public static async Task<AnalysisResult> analyze_code(
        [Description("The code to analyze")] string code,
        [Description("Programming language")] string language = "csharp")
    {
        var client = GetClient();
        var prompt = "Analyze this " + language + " code:\n\n```" + language + "\n" + code + "\n```\n\n" +
            "Review: Structure, Complexity, Patterns, Bugs, Security.\n\n" +
            "Respond in JSON: {\"complexityScore\": 5, \"complexityLevel\": \"medium\", \"detectedPatterns\": [], \"potentialIssues\": [], \"suggestions\": [], \"summary\": \"assessment\"}";

        try
        {
            var result = await client.GenerateAsync(prompt).StreamToEndAsync();
            var content = result?.Response ?? "";
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<AIAnalysisResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed is not null)
                {
                    return new AnalysisResult
                    {
                        Language = language,
                        ComplexityScore = parsed.ComplexityScore,
                        ComplexityLevel = parsed.ComplexityLevel ?? "medium",
                        DetectedPatterns = parsed.DetectedPatterns ?? [],
                        PotentialIssues = parsed.PotentialIssues ?? [],
                        Suggestions = parsed.Suggestions ?? [],
                        Summary = parsed.Summary ?? "",
                        AiPowered = true
                    };
                }
            }
            return new AnalysisResult { Language = language, Summary = content, ComplexityLevel = "unknown", AiPowered = false };
        }
        catch (Exception ex)
        {
            return new AnalysisResult { Language = language, Summary = "Error: " + ex.Message, AiPowered = false };
        }
    }

    [McpServerTool, Description("Uses AI to generate unit tests for code")]
    public static async Task<TestGenResult> generate_tests(
        [Description("The code to generate tests for")] string code,
        [Description("Test framework (xunit, nunit, mstest)")] string framework = "xunit")
    {
        var client = GetClient();
        var prompt = "Generate " + framework + " unit tests for:\n\n```\n" + code + "\n```\n\n" +
            "Cover: Happy paths, edge cases, error handling.\n\n" +
            "Respond in JSON: {\"testCode\": \"test code\", \"testCount\": 5, \"scenarios\": [\"scenario 1\"]}";

        try
        {
            var result = await client.GenerateAsync(prompt).StreamToEndAsync();
            var content = result?.Response ?? "";
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<AITestGenResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed is not null)
                {
                    return new TestGenResult { Success = true, Framework = framework, TestCode = parsed.TestCode ?? "", TestCount = parsed.TestCount, Scenarios = parsed.Scenarios ?? [], AiPowered = true };
                }
            }
            return new TestGenResult { Success = false, Framework = framework, Error = content, AiPowered = false };
        }
        catch (Exception ex)
        {
            return new TestGenResult { Success = false, Framework = framework, Error = "Error: " + ex.Message, AiPowered = false };
        }
    }

    [McpServerTool, Description("Uses AI to refactor code")]
    public static async Task<RefactorResult> refactor_code(
        [Description("The code to refactor")] string code,
        [Description("Goal: readability, performance, modularity")] string goal = "readability",
        [Description("Programming language")] string language = "csharp")
    {
        var client = GetClient();
        var prompt = "Refactor this " + language + " code to improve " + goal + ":\n\n```" + language + "\n" + code + "\n```\n\n" +
            "Respond in JSON: {\"refactoredCode\": \"code\", \"changes\": [\"change 1\"], \"improvementScore\": 80}";

        try
        {
            var result = await client.GenerateAsync(prompt).StreamToEndAsync();
            var content = result?.Response ?? "";
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<AIRefactorResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (parsed is not null)
                {
                    return new RefactorResult { Success = true, OriginalCode = code, RefactoredCode = parsed.RefactoredCode ?? "", Goal = goal, Changes = parsed.Changes ?? [], ImprovementScore = parsed.ImprovementScore, AiPowered = true };
                }
            }
            return new RefactorResult { Success = false, OriginalCode = code, Goal = goal, Error = content, AiPowered = false };
        }
        catch (Exception ex)
        {
            return new RefactorResult { Success = false, OriginalCode = code, Goal = goal, Error = "Error: " + ex.Message, AiPowered = false };
        }
    }

    private static IOllamaApiClient GetClient()
    {
        if (_ollamaClient == null)
        {
            _ollamaClient = new OllamaApiClient("http://localhost:11434");
            _ollamaClient.SelectedModel = "llama3.1";
        }
        return _ollamaClient;
    }
}
