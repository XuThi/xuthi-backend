using Cart.ShoppingCarts.Models;
using Core.DDD;

namespace Cart.Data;

public class CartDbContext(
    DbContextOptions<CartDbContext> options,
    DispatchDomainEventsInterceptor? interceptor = null)
    : DbContext(options)
{
    public DbSet<ShoppingCart> ShoppingCarts => Set<ShoppingCart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (interceptor is not null)
            optionsBuilder.AddInterceptors(interceptor);

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ShoppingCart>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.SessionId).IsUnique().HasFilter("\"SessionId\" IS NOT NULL");
            entity.HasIndex(c => c.CustomerId).HasFilter("\"CustomerId\" IS NOT NULL");
            entity.Property(c => c.SessionId).HasMaxLength(100);
            entity.Property(c => c.AppliedVoucherCode).HasMaxLength(50);
            entity.Property(c => c.VoucherDiscount).HasPrecision(18, 2);

            entity.HasMany(c => c.Items)
                .WithOne(i => i.Cart)
                .HasForeignKey(i => i.CartId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ignore computed properties
            entity.Ignore(c => c.Subtotal);
            entity.Ignore(c => c.Total);
            entity.Ignore(c => c.TotalItems);

            // DDD base class: map existing columns, ignore unused audit fields
            entity.Ignore(c => c.CreatedBy);
            entity.Ignore(c => c.UpdatedBy);
            entity.Property(c => c.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(c => c.UpdatedAt).HasColumnName("UpdatedAt");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => new { i.CartId, i.VariantId }).IsUnique();
            entity.Property(i => i.ProductName).HasMaxLength(200);
            entity.Property(i => i.VariantSku).HasMaxLength(100);
            entity.Property(i => i.VariantDescription).HasMaxLength(300);
            entity.Property(i => i.ImageUrl).HasMaxLength(500);
            entity.Property(i => i.UnitPrice).HasPrecision(18, 2);
            entity.Property(i => i.CompareAtPrice).HasPrecision(18, 2);

            // Ignore computed properties
            entity.Ignore(i => i.TotalPrice);
            entity.Ignore(i => i.IsOnSale);

            // DDD base class: map AddedAt column to CreatedAt, ignore unused
            entity.Ignore(i => i.CreatedBy);
            entity.Ignore(i => i.UpdatedBy);
            entity.Property(i => i.CreatedAt).HasColumnName("AddedAt");
            entity.Property(i => i.UpdatedAt).HasColumnName("UpdatedAt");
        });

        base.OnModelCreating(modelBuilder);
    }
}
