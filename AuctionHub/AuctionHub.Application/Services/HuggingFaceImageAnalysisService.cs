using System.Net.Http.Headers;
using System.Text.Json;
using AuctionHub.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AuctionHub.Application.Services;

public class HuggingFaceImageAnalysisService : IImageAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiToken;
    private readonly ILogger<HuggingFaceImageAnalysisService> _logger;
    
    // НОВИЯТ ОФИЦИАЛЕН АДРЕС (Inference Router)
    private const string ApiUrl = "https://router.huggingface.co/hf-inference/models/Falconsai/nsfw_image_detection";

    public HuggingFaceImageAnalysisService(HttpClient httpClient, IConfiguration configuration, ILogger<HuggingFaceImageAnalysisService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiToken = Environment.GetEnvironmentVariable("AI__HuggingFaceToken") 
                   ?? configuration["AI__HuggingFaceToken"] 
                   ?? string.Empty;
                   
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
    }

    public async Task<ImageAnalysisResult> AnalyzeImageAsync(Stream imageStream, string fileName)
    {
        var result = new ImageAnalysisResult { IsSafeForWork = true };

        if (string.IsNullOrEmpty(_apiToken))
        {
            _logger.LogWarning("Hugging Face API Token is missing. Skipping image analysis.");
            return result;
        }

        try
        {
            if (imageStream.CanSeek) imageStream.Position = 0;

            byte[] imageData;
            using (var ms = new MemoryStream())
            {
                await imageStream.CopyToAsync(ms);
                imageData = ms.ToArray();
            }

            // Изпращаме RAW BINARY данни (това е най-сигурният начин за новия рутер)
            var content = new ByteArrayContent(imageData);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);
            
            // Тези хедъри са препоръчителни за новия рутер
            if (!_httpClient.DefaultRequestHeaders.Contains("X-Wait-For-Model"))
                _httpClient.DefaultRequestHeaders.Add("X-Wait-For-Model", "true");
            
            if (!_httpClient.DefaultRequestHeaders.Contains("X-Use-Cache"))
                _httpClient.DefaultRequestHeaders.Add("X-Use-Cache", "true");

            var response = await _httpClient.PostAsync(ApiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("AI API returned {StatusCode}: {Error}. Skipping analysis for {FileName}.", 
                    response.StatusCode, errorBody, fileName);
                return result; 
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var classifications = JsonSerializer.Deserialize<List<HuggingFaceClassification>>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (classifications != null && classifications.Any())
            {
                foreach (var cls in classifications)
                {
                    result.DetectedCategories.Add($"{cls.Label}: {cls.Score:P1}");
                }

                var nsfwEntry = classifications.FirstOrDefault(c => c.Label.Equals("nsfw", StringComparison.OrdinalIgnoreCase));
                if (nsfwEntry != null && nsfwEntry.Score > 0.8) 
                {
                    result.IsSafeForWork = false;
                    result.FlaggedReason = $"Image content flagged as NSFW (Confidence: {nsfwEntry.Score:P0})";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during image analysis for {FileName}", fileName);
        }
        finally
        {
            if (imageStream.CanSeek) imageStream.Position = 0;
        }

        return result;
    }

    private class HuggingFaceClassification
    {
        public string Label { get; set; } = null!;
        public double Score { get; set; }
    }
}
