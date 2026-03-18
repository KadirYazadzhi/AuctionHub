using AuctionHub.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AuctionHub.Application.Services;

public class MockImageAnalysisService : IImageAnalysisService
{
    private readonly ILogger<MockImageAnalysisService> _logger;

    public MockImageAnalysisService(ILogger<MockImageAnalysisService> logger)
    {
        _logger = logger;
    }

    public async Task<ImageAnalysisResult> AnalyzeImageAsync(Stream imageStream, string fileName)
    {
        _logger.LogInformation($"[AI Mock] Analyzing image {fileName}...");
        
        // Simulate network/AI delay
        await Task.Delay(500);

        var lowerName = fileName.ToLower();
        
        // Simple mock rules for project demonstration
        var prohibitedKeywords = new[] { "nsfw", "weapon", "fake", "drugs", "money", "viagra", "adult", "explicit" };
        if (prohibitedKeywords.Any(k => lowerName.Contains(k)))
        {
            _logger.LogWarning($"[AI Mock] Image {fileName} flagged as unsafe!");
            return new ImageAnalysisResult
            {
                IsSafeForWork = false,
                FlaggedReason = "Automated AI moderation detected inappropriate or prohibited content."
            };
        }

        return new ImageAnalysisResult
        {
            IsSafeForWork = true,
            DetectedCategories = new List<string> { "Electronics", "General" }
        };
    }
}
