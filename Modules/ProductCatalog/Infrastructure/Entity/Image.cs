namespace ProductCatalog.Infrastructure.Entity;

public class Image
{
    public Guid Id { get; set; }
    public string Url { get; set; } = default!; // Full URL (Cloudinary or Firebase)
    public string? CloudinaryPublicId { get; set; } // For Cloudinary deletion (null for Firebase URLs)
    public string? AltText { get; set; }
}