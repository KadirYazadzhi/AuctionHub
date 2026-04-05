using AuctionHub.Application.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;

namespace AuctionHub.Infrastructure.Services;

public class PhotoService : IPhotoService
{
    private readonly Cloudinary? _cloudinary;

    public PhotoService(IConfiguration config)
    {
        var cloudName = config["Cloudinary:CloudName"];
        var apiKey = config["Cloudinary:ApiKey"];
        var apiSecret = config["Cloudinary:ApiSecret"];

        if (!string.IsNullOrEmpty(cloudName) && !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
        {
            var acc = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(acc);
        }
    }

    public async Task<(bool Success, string Url, string PublicId)> AddPhotoAsync(Stream fileStream, string fileName)
    {
        if (_cloudinary == null)
        {
            return (false, string.Empty, string.Empty);
        }

        var uploadResult = new ImageUploadResult();

        if (fileStream.Length > 0)
        {
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, fileStream),
                Transformation = new Transformation().Height(500).Width(800).Crop("fill").Gravity("face")
            };
            uploadResult = await _cloudinary.UploadAsync(uploadParams);
        }

        if (uploadResult.Error != null)
        {
            return (false, string.Empty, string.Empty);
        }

        return (true, uploadResult.SecureUrl.ToString(), uploadResult.PublicId);
    }

    public async Task<(bool Success, string Message)> DeletePhotoAsync(string publicId)
    {
        if (_cloudinary == null)
        {
            return (false, "Cloudinary service not configured.");
        }

        var deleteParams = new DeletionParams(publicId);
        var result = await _cloudinary.DestroyAsync(deleteParams);

        return result.Result == "ok" 
            ? (true, "Deleted") 
            : (false, result.Error?.Message ?? "Error deleting photo");
    }

    public (bool Success, string ErrorMessage) ValidateImage(long length, string contentType, string fileName)
    {
        if (length > 5 * 1024 * 1024)
        {
            return (false, "File size must be less than 5MB.");
        }

        var allowedMimeTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
        if (!string.IsNullOrEmpty(contentType) && !allowedMimeTypes.Contains(contentType.ToLowerInvariant()))
        {
            return (false, "Invalid file type. Must be an image (JPEG, PNG, GIF, or WebP).");
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            return (false, "Invalid file extension. Allowed: jpg, jpeg, png, gif, webp.");
        }

        return (true, string.Empty);
    }

    public void DeleteLocalImage(string? imageUrl, string webRootPath)
    {
        if (string.IsNullOrEmpty(imageUrl)) return;
        
        if (imageUrl.StartsWith("/images/auctions/"))
        {
            var relativePath = imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var filePath = Path.Combine(webRootPath, relativePath);
            
            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    System.IO.File.Delete(filePath);
                }
                catch
                {
                    // Log error or ignore
                }
            }
        }
    }
}
