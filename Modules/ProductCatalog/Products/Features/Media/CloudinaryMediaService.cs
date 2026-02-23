using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace ProductCatalog.Products.Features.Media;

public interface ICloudinaryMediaService
{
    Task<(string Url, string? PublicId)> UploadImageAsync(IFormFile file, string folder, CancellationToken cancellationToken);
    Task DeleteImageAsync(string? publicId, CancellationToken cancellationToken);
}

internal class CloudinaryMediaService(
    IConfiguration configuration) : ICloudinaryMediaService
{
    public async Task<(string Url, string? PublicId)> UploadImageAsync(
        IFormFile file,
        string folder,
        CancellationToken cancellationToken)
    {
        var settings = ReadCloudinarySettings(configuration)
            ?? throw new InvalidOperationException("Thiếu cấu hình Cloudinary trên backend.");

        var cloudinary = CreateCloudinaryClient(settings);
        await using var fileStream = file.OpenReadStream();

        ImageUploadResult result;
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, fileStream),
            Folder = folder,
        };

        if (!string.IsNullOrWhiteSpace(settings.UploadPreset))
            uploadParams.UploadPreset = settings.UploadPreset;

        result = await cloudinary.UploadAsync(uploadParams, cancellationToken);

        if (result.Error is not null)
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");

        if (string.IsNullOrWhiteSpace(result.SecureUrl?.ToString()))
            throw new InvalidOperationException("Cloudinary did not return secure_url.");

        return (result.SecureUrl!.ToString(), result.PublicId);
    }

    public async Task DeleteImageAsync(string? publicId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(publicId))
            return;

        var settings = ReadCloudinarySettings(configuration);
        if (settings is null)
            return;

        var cloudinary = CreateCloudinaryClient(settings.Value);
        var result = await cloudinary.DestroyAsync(new DeletionParams(publicId));

        if (result.Error is not null)
            throw new InvalidOperationException($"Cloudinary delete failed: {result.Error.Message}");
    }

    private static (string CloudName, string ApiKey, string ApiSecret, string? UploadPreset)? ReadCloudinarySettings(IConfiguration cfg)
    {
        var cloudName = cfg["Cloudinary:CloudName"];
        var apiKey = cfg["Cloudinary:ApiKey"];
        var apiSecret = cfg["Cloudinary:ApiSecret"];
        var uploadPreset = cfg["Cloudinary:UploadPreset"];

        if (!string.IsNullOrWhiteSpace(cloudName)
            && !string.IsNullOrWhiteSpace(apiKey)
            && !string.IsNullOrWhiteSpace(apiSecret))
            return (cloudName, apiKey, apiSecret, uploadPreset);

        var cloudinaryUrl = cfg["CLOUDINARY_URL"];
        if (!string.IsNullOrWhiteSpace(cloudinaryUrl)
            && Uri.TryCreate(cloudinaryUrl, UriKind.Absolute, out var cloudinaryUri)
            && cloudinaryUri.Scheme.Equals("cloudinary", StringComparison.OrdinalIgnoreCase))
        {
            var userInfo = cloudinaryUri.UserInfo.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
            if (userInfo.Length == 2 && !string.IsNullOrWhiteSpace(cloudinaryUri.Host))
                return (cloudinaryUri.Host, userInfo[0], userInfo[1], uploadPreset);
        }

        return null;
    }

    private static Cloudinary CreateCloudinaryClient((string CloudName, string ApiKey, string ApiSecret, string? UploadPreset) settings)
    {
        var account = new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret);
        return new Cloudinary(account);
    }
}
