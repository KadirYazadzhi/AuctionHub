namespace AuctionHub.Application.Interfaces;

public interface IPhotoService
{
    Task<(bool Success, string Url, string PublicId)> AddPhotoAsync(Stream fileStream, string fileName);
    Task<(bool Success, string Message)> DeletePhotoAsync(string publicId);
    (bool Success, string ErrorMessage) ValidateImage(long length, string contentType, string fileName);
    void DeleteLocalImage(string? imageUrl, string webRootPath);
}
