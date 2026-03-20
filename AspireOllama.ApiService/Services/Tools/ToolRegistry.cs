using Microsoft.Extensions.AI;

namespace AspireOllama.ApiService.Services.Tools;

public class ToolRegistry : IToolRegistry
{
    private readonly List<AIFunction> _tools = new();
    private readonly ILogger<ToolRegistry> _logger;

    public ToolRegistry(
        CalculatorTool calculatorTool,
        WebSearchTool webSearchTool,
        CodeExecutionTool codeExecutionTool,
        FileOperationsTool fileOperationsTool,
        RagSearchTool ragSearchTool,
        VisionTool visionTool,
        ILogger<ToolRegistry> logger)
    {
        _logger = logger;

        if (calculatorTool.IsEnabled)
        {
            _tools.Add(AIFunctionFactory.Create(calculatorTool.Calculate, "calculator"));
            _logger.LogInformation("Registered tool: calculator");
        }

        if (webSearchTool.IsEnabled)
        {
            _tools.Add(AIFunctionFactory.Create(webSearchTool.SearchAsync, "web_search"));
            _logger.LogInformation("Registered tool: web_search");
        }

        if (ragSearchTool.IsEnabled)
        {
            _tools.Add(AIFunctionFactory.Create(ragSearchTool.SearchAsync, "search_knowledge_base"));
            _logger.LogInformation("Registered tool: search_knowledge_base");
        }

        if (visionTool.IsEnabled)
        {
            _tools.Add(AIFunctionFactory.Create(visionTool.AnalyzeAsync, "analyze_image"));
            _logger.LogInformation("Registered tool: analyze_image");
        }

        if (codeExecutionTool.IsEnabled)
        {
            _tools.Add(AIFunctionFactory.Create(codeExecutionTool.ExecuteAsync, "execute_code"));
            _logger.LogInformation("Registered tool: execute_code");
        }

        if (fileOperationsTool.IsEnabled)
        {
            _tools.Add(AIFunctionFactory.Create(fileOperationsTool.ListFiles, "list_files"));
            _tools.Add(AIFunctionFactory.Create(fileOperationsTool.ReadFile, "read_file"));
            _tools.Add(AIFunctionFactory.Create(fileOperationsTool.WriteFile, "write_file"));
            _logger.LogInformation("Registered tool: file_operations (3 functions)");
        }

        _logger.LogInformation("Tool registry initialized with {Count} tools", _tools.Count);
    }

    public IReadOnlyList<AIFunction> GetEnabledTools() => _tools.AsReadOnly();

    public void RegisterTool(AIFunction tool)
    {
        _tools.Add(tool);
        _logger.LogInformation("Dynamically registered tool: {Name}", tool.Name);
    }
}
