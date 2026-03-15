using Microsoft.Extensions.AI;

namespace AspireOllama.ApiService.Services.Tools;

/// <summary>
/// Registry for collecting and managing all available AI tools.
/// Converts tool methods to AIFunction instances using AIFunctionFactory.
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private readonly List<AIFunction> _tools = new();
    private readonly ILogger<ToolRegistry> _logger;

    public ToolRegistry(
        CalculatorTool calculatorTool,
        WebSearchTool webSearchTool,
        CodeExecutionTool codeExecutionTool,
        FileOperationsTool fileOperationsTool,
        ILogger<ToolRegistry> logger)
    {
        _logger = logger;

        // Register Calculator tool
        if (calculatorTool.IsEnabled)
        {
            _tools.Add(AIFunctionFactory.Create(calculatorTool.Calculate, "calculator"));
            _logger.LogInformation("Registered tool: {Name}", calculatorTool.Name);
        }

        // Register Web Search tool
        if (webSearchTool.IsEnabled)
        {
            _tools.Add(AIFunctionFactory.Create(webSearchTool.SearchAsync, "web_search"));
            _logger.LogInformation("Registered tool: {Name}", webSearchTool.Name);
        }

        // Register Code Execution tool
        if (codeExecutionTool.IsEnabled)
        {
            _tools.Add(AIFunctionFactory.Create(codeExecutionTool.ExecuteAsync, "execute_code"));
            _logger.LogInformation("Registered tool: {Name}", codeExecutionTool.Name);
        }

        // Register File Operations tools
        if (fileOperationsTool.IsEnabled)
        {
            _tools.Add(AIFunctionFactory.Create(fileOperationsTool.ListFiles, "list_files"));
            _tools.Add(AIFunctionFactory.Create(fileOperationsTool.ReadFile, "read_file"));
            _tools.Add(AIFunctionFactory.Create(fileOperationsTool.WriteFile, "write_file"));
            _logger.LogInformation("Registered tool: {Name} (3 functions)", fileOperationsTool.Name);
        }

        _logger.LogInformation("Tool registry initialized with {Count} tools", _tools.Count);
    }

    /// <inheritdoc />
    public IReadOnlyList<AIFunction> GetEnabledTools()
    {
        return _tools.AsReadOnly();
    }

    /// <inheritdoc />
    public void RegisterTool(AIFunction tool)
    {
        _tools.Add(tool);
        _logger.LogInformation("Dynamically registered tool: {Name}", tool.Name);
    }
}
