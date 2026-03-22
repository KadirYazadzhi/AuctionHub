using AuctionHub.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace AuctionHub.Application.Services;

public class LocalAIImageAnalysisService : IImageAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LocalAIImageAnalysisService> _logger;
    private readonly string _aiServiceUrl;

    public LocalAIImageAnalysisService(
        HttpClient httpClient, 
        IConfiguration configuration,
        ILogger<LocalAIImageAnalysisService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _aiServiceUrl = configuration["AI:ModerationServiceUrl"] ?? "http://ai-moderator:5000/analyze";
    }

    public async Task<ImageAnalysisResult> AnalyzeImageAsync(Stream imageStream, string fileName)
    {
        try
        {
            _logger.LogInformation($"Sending image {fileName} to local AI cluster at {_aiServiceUrl}...");

            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(imageStream);
            content.Add(streamContent, "image", fileName); // API expects 'image' field

            var response = await _httpClient.PostAsync(_aiServiceUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"AI Service returned error: {response.StatusCode}");
                return new ImageAnalysisResult { IsSafeForWork = true };
            }

            var result = await response.Content.ReadFromJsonAsync<AIResponse>();

            if (result == null) return new ImageAnalysisResult { IsSafeForWork = true };

            // Logic: Threshold for unsafe content
            bool isUnsafe = result.Porn > 0.70m || result.Hentai > 0.70m || result.Sexy > 0.85m;

            if (isUnsafe)
            {
                _logger.LogWarning($"AI flagged image {fileName} as UNSAFE. Porn: {result.Porn:P}, Hentai: {result.Hentai:P}, Sexy: {result.Sexy:P}");
                return new ImageAnalysisResult
                {
                    IsSafeForWork = false,
                    FlaggedReason = "Automated AI moderation detected inappropriate content."
                };
            }

            return new ImageAnalysisResult { IsSafeForWork = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Local AI Cluster.");
            return new ImageAnalysisResult { IsSafeForWork = true };
        }
    }

    private class AIResponse
    {
        public decimal Porn { get; set; }
        public decimal Hentai { get; set; }
        public decimal Sexy { get; set; }
        public decimal Neutral { get; set; }
        public decimal Drawings { get; set; }
    }
}
