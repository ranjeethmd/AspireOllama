using System.ComponentModel;
using AspireOllama.Shared;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AspireOllama.ApiService.Services.Tools;

/// <summary>
/// Tool for analyzing images using LLaVA vision model.
/// This allows Llama 3 to act as the main agent and delegate image processing to LLaVA.
/// </summary>
public class ImageAnalysisTool : ITool
{
    private readonly IChatClient _visionClient;
    private readonly ILogger<ImageAnalysisTool> _logger;
    private readonly bool _isEnabled;

    // Instance storage for pending images (scoped per request)
    private List<PendingImage> _pendingImages = new();

    public string Name => "image_analysis";
    public string Description => "Analyzes images using vision AI to describe content, extract text, identify objects, and answer questions about images";
    public bool IsEnabled => _isEnabled;

    public ImageAnalysisTool(
        [FromKeyedServices("vision")] IChatClient visionClient,
        IOptions<ToolConfiguration> config,
        ILogger<ImageAnalysisTool> logger)
    {
        _visionClient = visionClient;
        _logger = logger;
        _isEnabled = config.Value.EnableImageAnalysis;
    }

    /// <summary>
    /// Sets images for analysis.
    /// </summary>
    public void SetImages(List<PendingImage> images)
    {
        _pendingImages = images;
        _logger.LogInformation("ImageAnalysisTool: Set {Count} images", images.Count);
    }

    /// <summary>
    /// Clears pending images.
    /// </summary>
    public void ClearImages()
    {
        _pendingImages = new List<PendingImage>();
    }

    /// <summary>
    /// Analyzes all uploaded images with a specific question or instruction.
    /// </summary>
    /// <param name="instruction">The question or instruction about the image(s).</param>
    /// <returns>The analysis result from the vision model.</returns>
    [Description("Analyzes images using AI vision. Use this tool when the user uploads images and wants you to describe them, extract text, identify objects, answer questions about them, or understand visual content. This tool processes all uploaded images.")]
    public async Task<string> AnalyzeImageAsync(
        [Description("The question or instruction about the image(s). Examples: 'Describe this image', 'What text is visible?', 'Identify objects', 'What is shown in this diagram?'")]
        string instruction,
        CancellationToken cancellationToken = default)
    {
        if (_pendingImages.Count == 0)
        {
            _logger.LogWarning("AnalyzeImage called but no images available");
            return "No images are available for analysis. Please ask the user to upload an image first.";
        }

        _logger.LogInformation("Analyzing {Count} image(s) with instruction: {Instruction}",
            _pendingImages.Count, instruction);

        try
        {
            var contentParts = new List<AIContent>();

            // Add all images to analyze
            foreach (var image in _pendingImages)
            {
                contentParts.Add(new DataContent(image.Data, image.MediaType));
                _logger.LogInformation("Adding image for analysis: {FileName}", image.FileName);
            }

            // Add the instruction text
            contentParts.Add(new TextContent(instruction));

            var messages = new List<ChatMessage>
            {
                new(ChatRole.User, contentParts)
            };

            // Call LLaVA for image analysis
            var response = await _visionClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
            var result = response.Text ?? "Unable to analyze the image.";

            _logger.LogInformation("Image analysis completed successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze image");
            return $"Failed to analyze image: {ex.Message}";
        }
    }
}

/// <summary>
/// Represents a pending image for analysis.
/// </summary>
public class PendingImage
{
    public required string FileName { get; set; }
    public required byte[] Data { get; set; }
    public required string MediaType { get; set; }
}
