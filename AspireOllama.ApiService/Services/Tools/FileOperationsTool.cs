using AspireOllama.Shared;
using Microsoft.Extensions.Options;
using System.ComponentModel;

namespace AspireOllama.ApiService.Services.Tools;

/// <summary>
/// Tool for performing safe file operations within a sandboxed directory.
/// All operations are restricted to the configured sandbox path.
/// </summary>
public class FileOperationsTool : ITool
{
    private readonly ILogger<FileOperationsTool> _logger;
    private readonly string _sandboxPath;
    private readonly bool _isEnabled;
    private readonly HashSet<string> _allowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".json", ".md", ".csv", ".xml", ".yaml", ".yml", ".log"
    };

    public string Name => "file_operations";
    public string Description => "Performs safe file operations in a sandboxed directory";
    public bool IsEnabled => _isEnabled;

    public FileOperationsTool(IOptions<ToolConfiguration> config, ILogger<FileOperationsTool> logger)
    {
        _logger = logger;
        _isEnabled = config.Value.EnableFileOperations;
        _sandboxPath = Path.GetFullPath(config.Value.SandboxPath);

        // Ensure sandbox directory exists
        if (_isEnabled && !Directory.Exists(_sandboxPath))
        {
            Directory.CreateDirectory(_sandboxPath);
            _logger.LogInformation("Created sandbox directory: {Path}", _sandboxPath);
        }
    }

    /// <summary>
    /// Lists files and directories in the sandbox.
    /// </summary>
    [Description("Lists files and directories in the sandbox directory. Use this to see what files are available.")]
    public string ListFiles(
        [Description("Optional subdirectory path within the sandbox")]
        string? subPath = null)
    {
        _logger.LogInformation("Listing files in sandbox: {SubPath}", subPath ?? "(root)");

        try
        {
            var targetPath = GetSafePath(subPath);

            if (!Directory.Exists(targetPath))
            {
                return $"Directory does not exist: {subPath ?? "(sandbox root)"}";
            }

            var dirs = Directory.GetDirectories(targetPath)
                .Select(d => $"[DIR] {Path.GetFileName(d)}");

            var files = Directory.GetFiles(targetPath)
                .Select(f => $"[FILE] {Path.GetFileName(f)} ({new FileInfo(f).Length} bytes)");

            var items = dirs.Concat(files).ToList();

            if (items.Count == 0)
            {
                return "Directory is empty.";
            }

            return string.Join(Environment.NewLine, items);
        }
        catch (UnauthorizedAccessException)
        {
            return "Error: Access denied - path is outside sandbox.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files");
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Reads the contents of a file in the sandbox.
    /// </summary>
    [Description("Reads and returns the contents of a text file from the sandbox directory.")]
    public string ReadFile(
        [Description("The filename or relative path to read")]
        string fileName)
    {
        _logger.LogInformation("Reading file: {FileName}", fileName);

        try
        {
            var filePath = GetSafePath(fileName);

            if (!File.Exists(filePath))
            {
                return $"File not found: {fileName}";
            }

            var extension = Path.GetExtension(filePath);
            if (!_allowedExtensions.Contains(extension))
            {
                return $"Error: File type '{extension}' is not allowed. Allowed types: {string.Join(", ", _allowedExtensions)}";
            }

            var content = File.ReadAllText(filePath);

            // Limit content size
            const int maxLength = 10000;
            if (content.Length > maxLength)
            {
                content = content[..maxLength] + $"\n\n... (truncated, showing first {maxLength} characters)";
            }

            return content;
        }
        catch (UnauthorizedAccessException)
        {
            return "Error: Access denied - path is outside sandbox.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file: {FileName}", fileName);
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Writes content to a file in the sandbox.
    /// </summary>
    [Description("Writes content to a text file in the sandbox directory. Creates the file if it doesn't exist.")]
    public string WriteFile(
        [Description("The filename or relative path to write to")]
        string fileName,
        [Description("The content to write to the file")]
        string content)
    {
        _logger.LogInformation("Writing file: {FileName}, Content length: {Length}", fileName, content?.Length ?? 0);

        try
        {
            var filePath = GetSafePath(fileName);

            var extension = Path.GetExtension(filePath);
            if (!_allowedExtensions.Contains(extension))
            {
                return $"Error: File type '{extension}' is not allowed. Allowed types: {string.Join(", ", _allowedExtensions)}";
            }

            // Ensure parent directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, content ?? string.Empty);

            var info = new FileInfo(filePath);
            return $"Successfully wrote {info.Length} bytes to {fileName}";
        }
        catch (UnauthorizedAccessException)
        {
            return "Error: Access denied - path is outside sandbox.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing file: {FileName}", fileName);
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Validates and returns a safe path within the sandbox.
    /// </summary>
    private string GetSafePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return _sandboxPath;
        }

        // Normalize and combine paths
        var fullPath = Path.GetFullPath(Path.Combine(_sandboxPath, relativePath));

        // Security check: ensure path is within sandbox
        if (!fullPath.StartsWith(_sandboxPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Access denied: Path is outside sandbox.");
        }

        return fullPath;
    }
}
