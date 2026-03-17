namespace AuctionHub.Application.Interfaces;

public interface IImageAnalysisService
{
    Task<ImageAnalysisResult> AnalyzeImageAsync(Stream imageStream, string fileName);
}

public class ImageAnalysisResult
{
    public bool IsSafeForWork { get; set; }
    public string? FlaggedReason { get; set; }
    public List<string> DetectedCategories { get; set; } = new();
}
