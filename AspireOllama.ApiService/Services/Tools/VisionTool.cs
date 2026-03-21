using AspireOllama.ApiService.Services.Message;
using AspireOllama.Shared;
using Microsoft.Extensions.AI;
using System.ComponentModel;

namespace AspireOllama.ApiService.Services.Tools;

public class VisionTool : ITool
{
    private readonly IChatClient _visionClient;
    private readonly IChatMessageService _messageService;
    private readonly ILogger<VisionTool> _logger;
    private List<ImageAttachment>? _currentRequestImages;

    public string Name => "analyze_image";
    public string Description => "Analyze images from the conversation using vision AI";
    public bool IsEnabled => true;

    public VisionTool(
        [FromKeyedServices(OllamaModels.VisionServiceKey)] IChatClient visionClient,
        IChatMessageService messageService,
        ILogger<VisionTool> logger)
    {
        _visionClient = visionClient;
        _messageService = messageService;
        _logger = logger;
    }

    public void SetCurrentImages(List<ImageAttachment>? images) => _currentRequestImages = images;
    public void ClearCurrentImages() => _currentRequestImages = null;

    [Description("Analyze images from the conversation. Retrieves the most recent images from the chat session and answers questions about them.")]
    public async Task<string> AnalyzeAsync(
        [Description("The chat session ID")] string session_id,
        [Description("What to analyze or describe about the image(s)")] string instruction,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Image tool: session={SessionId}, instruction={Instruction}", session_id, instruction);

        List<ImageAttachment> images;
        if (_currentRequestImages is { Count: > 0 })
        {
            images = _currentRequestImages;
        }
        else
        {
            var sessionHistory = await _messageService.GetBySessionIdAsync(session_id);
            var lastImageMsg = sessionHistory.LastOrDefault(m => m.Images.Count > 0);
            if (lastImageMsg is null)
                return "No images found in this conversation. Ask the user to upload an image.";
            images = lastImageMsg.Images;
        }

        var contentParts = new List<AIContent>();
        foreach (var img in images)
        {
            var data = Convert.FromBase64String(img.Base64Data);
            contentParts.Add(new DataContent(data, NormalizeMediaType(img.ContentType)));
        }
        contentParts.Add(new TextContent(instruction));

        var visionMessages = new List<ChatMessage> { new(ChatRole.User, contentParts) };

        _logger.LogInformation("Sending {Count} images to Qwen2.5-VL", images.Count);
        var visionResponse = await _visionClient.GetResponseAsync(visionMessages, cancellationToken: ct);
        return visionResponse.Text ?? "Unable to analyze the image.";
    }

    private static string NormalizeMediaType(string contentType) => contentType switch
    {
        "image/jpeg" or "image/jpg" => "image/jpeg",
        "image/png" => "image/png",
        "image/gif" => "image/gif",
        "image/webp" => "image/webp",
        _ => "image/jpeg"
    };
}
