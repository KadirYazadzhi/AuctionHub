using AuctionHub.Application.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;

namespace AuctionHub.Infrastructure.Services;

public class PhotoService : IPhotoService
{
    private readonly Cloudinary _cloudinary;

    public PhotoService(IConfiguration config)
    {
        var acc = new Account(
            config["Cloudinary:CloudName"],
            config["Cloudinary:ApiKey"],
            config["Cloudinary:ApiSecret"]
        );

        _cloudinary = new Cloudinary(acc);
    }

    public async Task<(bool Success, string Url, string PublicId)> AddPhotoAsync(Stream fileStream, string fileName)
    {
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
        var deleteParams = new DeletionParams(publicId);
        var result = await _cloudinary.DestroyAsync(deleteParams);

        return result.Result == "ok" 
            ? (true, "Deleted") 
            : (false, result.Error?.Message ?? "Error deleting photo");
    }
}
