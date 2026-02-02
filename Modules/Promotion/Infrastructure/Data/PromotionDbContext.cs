using Promotion.Infrastructure.Entity;

namespace Promotion.Infrastructure.Data;

public class PromotionDbContext : DbContext
{
    public PromotionDbContext(DbContextOptions<PromotionDbContext> options) : base(options)
    {
    }

    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<VoucherUsage> VoucherUsages => Set<VoucherUsage>();
    public DbSet<SaleCampaign> SaleCampaigns => Set<SaleCampaign>();
    public DbSet<SaleCampaignItem> SaleCampaignItems => Set<SaleCampaignItem>();
    
    // Backward-compatible aliases
    public DbSet<FlashSale> FlashSales => Set<FlashSale>();
    public DbSet<FlashSaleItem> FlashSaleItems => Set<FlashSaleItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Voucher
        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.HasIndex(v => v.Code).IsUnique();
            entity.Property(v => v.Code).HasMaxLength(50).IsRequired();
            entity.Property(v => v.Description).HasMaxLength(500);
            entity.Property(v => v.InternalNote).HasMaxLength(1000);
            entity.Property(v => v.DiscountValue).HasPrecision(18, 2);
            entity.Property(v => v.MinimumOrderAmount).HasPrecision(18, 2);
            entity.Property(v => v.MaximumDiscountAmount).HasPrecision(18, 2);
            
            // Store list of product IDs as JSON
            entity.Property(v => v.ApplicableProductIds)
                .HasConversion(
                    v => v == null ? null : string.Join(',', v),
                    v => v == null ? null : v.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList()
                )
                .Metadata.SetValueComparer(
                    new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<Guid>?>(
                        (c1, c2) => c1 != null && c2 != null ? c1.SequenceEqual(c2) : c1 == c2,
                        c => c == null ? 0 : c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                        c => c == null ? null : c.ToList()
                    )
                );
        });

        // VoucherUsage
        modelBuilder.Entity<VoucherUsage>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => new { u.VoucherId, u.CustomerId });
            entity.HasIndex(u => u.OrderId);
            entity.Property(u => u.DiscountApplied).HasPrecision(18, 2);
            
            entity.HasOne(u => u.Voucher)
                .WithMany()
                .HasForeignKey(u => u.VoucherId);
        });

        // SaleCampaign (was FlashSale)
        modelBuilder.Entity<SaleCampaign>(entity =>
        {
            entity.ToTable("SaleCampaigns");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name).HasMaxLength(200).IsRequired();
            entity.Property(s => s.Slug).HasMaxLength(200);
            entity.HasIndex(s => s.Slug).IsUnique();
            entity.Property(s => s.Description).HasMaxLength(1000);
            entity.Property(s => s.BannerImageUrl).HasMaxLength(500);
            entity.Property(s => s.Type).HasConversion<int>();
            
            entity.HasMany(s => s.Items)
                .WithOne(i => i.SaleCampaign)
                .HasForeignKey(i => i.SaleCampaignId);
        });

        // SaleCampaignItem (was FlashSaleItem)
        modelBuilder.Entity<SaleCampaignItem>(entity =>
        {
            entity.ToTable("SaleCampaignItems");
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => new { i.SaleCampaignId, i.ProductId, i.VariantId }).IsUnique();
            entity.Property(i => i.SalePrice).HasPrecision(18, 2);
            entity.Property(i => i.OriginalPrice).HasPrecision(18, 2);
            entity.Property(i => i.DiscountPercentage).HasPrecision(5, 2);
        });

        base.OnModelCreating(modelBuilder);
    }
}
