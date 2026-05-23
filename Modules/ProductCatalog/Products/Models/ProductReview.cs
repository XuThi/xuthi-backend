using Core.DDD;

namespace ProductCatalog.Products.Models;

/// <summary>
/// A user review for a product. Rating 1–5.
/// AverageRating and ReviewCount on the parent Product are
/// re-calculated and stored every time a new review is submitted.
/// </summary>
public class ProductReview : Entity<Guid>
{
    public Guid ProductId { get; set; }

    // Reviewer identity (optional — guest reviews allowed)
    public Guid? CustomerId { get; set; }

    /// <summary>Display name shown on the review card.</summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>Email — stored for moderation but never surfaced publicly.</summary>
    public string AuthorEmail { get; set; } = string.Empty;

    /// <summary>Star rating from 1 (worst) to 5 (best).</summary>
    public int Rating { get; set; }

    /// <summary>Written review body (optional).</summary>
    public string? Comment { get; set; }

    /// <summary>Soft approval gate — default false so reviews don't go live immediately.</summary>
    public bool IsApproved { get; set; } = true;

    // Navigation
    public Product Product { get; set; } = null!;
}
