using Cart.Infrastructure.Entity;

namespace Cart.Infrastructure.Data;

public class CartDbContext : DbContext
{
    public CartDbContext(DbContextOptions<CartDbContext> options) : base(options)
    {
    }

    public DbSet<ShoppingCart> ShoppingCarts => Set<ShoppingCart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

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
        });

        base.OnModelCreating(modelBuilder);
    }
}
