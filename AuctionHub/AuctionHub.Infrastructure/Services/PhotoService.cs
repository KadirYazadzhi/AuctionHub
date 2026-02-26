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
}
