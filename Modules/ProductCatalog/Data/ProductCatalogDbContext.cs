using Core.DDD;
using Microsoft.EntityFrameworkCore;
using ProductCatalog.Brands.Models;
using ProductCatalog.Categories.Models;
using ProductCatalog.Groups.Models;
using ProductCatalog.Products.Models;
using ProductCatalog.VariantOptions.Models;

namespace ProductCatalog.Data;

public class ProductCatalogDbContext(
    DbContextOptions<ProductCatalogDbContext> options,
    DispatchDomainEventsInterceptor? interceptor = null)
    : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupProduct> GroupProducts => Set<GroupProduct>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Image> Images => Set<Image>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Variant> Variants => Set<Variant>();
    public DbSet<VariantImage> VariantImages => Set<VariantImage>();
    public DbSet<VariantOption> VariantOptions => Set<VariantOption>();
    public DbSet<VariantOptionValue> VariantOptionValues => Set<VariantOptionValue>();
    public DbSet<ProductVariantOption> ProductVariantOptions => Set<ProductVariantOption>();
    public DbSet<VariantOptionSelection> VariantOptionSelections => Set<VariantOptionSelection>();
    public DbSet<OrderItemProductReference> OrderItemProductReferences => Set<OrderItemProductReference>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (interceptor is not null)
            optionsBuilder.AddInterceptors(interceptor);

        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Product
        modelBuilder.Entity<Product>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.UrlSlug).IsUnique();
            e.HasOne(p => p.Brand).WithMany().HasForeignKey(p => p.BrandId);
            e.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId);
            e.HasMany(p => p.Variants).WithOne().HasForeignKey(v => v.ProductId);
            e.HasMany(p => p.Images).WithOne().HasForeignKey(pi => pi.ProductId);
            e.HasMany(p => p.VariantOptions).WithOne().HasForeignKey(pvo => pvo.ProductId);
            // Ignore DDD base class audit fields not used in this module yet
            e.Ignore(p => p.CreatedBy);
            e.Ignore(p => p.UpdatedBy);
            e.Property(p => p.CreatedAt).HasColumnName("CreatedAt");
            e.Property(p => p.UpdatedAt).HasColumnName("UpdatedAt");
        });

        // Category
        modelBuilder.Entity<Category>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.UrlSlug).IsUnique();
            e.Ignore(c => c.CreatedAt);
            e.Ignore(c => c.CreatedBy);
            e.Ignore(c => c.UpdatedAt);
            e.Ignore(c => c.UpdatedBy);
        });

        // Group
        modelBuilder.Entity<Group>(e =>
        {
            e.HasKey(g => g.Id);
            e.Ignore(g => g.CreatedAt);
            e.Ignore(g => g.CreatedBy);
            e.Ignore(g => g.UpdatedAt);
            e.Ignore(g => g.UpdatedBy);
        });

        // GroupProduct (join table)
        modelBuilder.Entity<GroupProduct>(e =>
        {
            e.HasKey(gp => new { gp.GroupId, gp.ProductId });
        });

        // Brand
        modelBuilder.Entity<Brand>(e =>
        {
            e.HasKey(b => b.Id);
            e.HasIndex(b => b.UrlSlug).IsUnique();
            e.Ignore(b => b.CreatedAt);
            e.Ignore(b => b.CreatedBy);
            e.Ignore(b => b.UpdatedAt);
            e.Ignore(b => b.UpdatedBy);
        });

        // Image
        modelBuilder.Entity<Image>(e =>
        {
            e.HasKey(i => i.Id);
            e.Ignore(i => i.CreatedAt);
            e.Ignore(i => i.CreatedBy);
            e.Ignore(i => i.UpdatedAt);
            e.Ignore(i => i.UpdatedBy);
        });

        // ProductImage
        modelBuilder.Entity<ProductImage>(e =>
        {
            e.HasKey(pi => pi.Id);
            e.HasOne(pi => pi.Image).WithMany().HasForeignKey(pi => pi.ImageId);
            e.Ignore(pi => pi.CreatedAt);
            e.Ignore(pi => pi.CreatedBy);
            e.Ignore(pi => pi.UpdatedAt);
            e.Ignore(pi => pi.UpdatedBy);
        });

        // Variant
        modelBuilder.Entity<Variant>(e =>
        {
            e.HasKey(v => v.Id);
            e.HasIndex(v => v.Sku).IsUnique();
            e.HasMany(v => v.OptionSelections).WithOne().HasForeignKey(os => os.VariantId);
            e.HasMany(v => v.Images).WithOne().HasForeignKey(vi => vi.VariantId);
            e.Ignore(v => v.CreatedBy);
            e.Ignore(v => v.UpdatedBy);
            e.Property(v => v.CreatedAt).HasColumnName("CreatedAt");
            e.Property(v => v.UpdatedAt).HasColumnName("UpdatedAt");
        });

        // VariantImage
        modelBuilder.Entity<VariantImage>(e =>
        {
            e.HasKey(vi => vi.Id);
            e.HasOne(vi => vi.Image).WithMany().HasForeignKey(vi => vi.ImageId);
            e.Ignore(vi => vi.CreatedAt);
            e.Ignore(vi => vi.CreatedBy);
            e.Ignore(vi => vi.UpdatedAt);
            e.Ignore(vi => vi.UpdatedBy);
        });

        // VariantOption
        modelBuilder.Entity<VariantOption>(e =>
        {
            e.HasKey(vo => vo.Id);
            e.Property(vo => vo.Id).ValueGeneratedNever();
            e.HasMany(vo => vo.Values).WithOne().HasForeignKey(vov => vov.VariantOptionId);
        });

        // VariantOptionValue
        modelBuilder.Entity<VariantOptionValue>(e =>
        {
            e.HasKey(vov => vov.Id);
        });

        // ProductVariantOption (join table)
        modelBuilder.Entity<ProductVariantOption>(e =>
        {
            e.HasKey(pvo => new { pvo.ProductId, pvo.VariantOptionId });
        });

        // VariantOptionSelection
        modelBuilder.Entity<VariantOptionSelection>(e =>
        {
            e.HasKey(vos => new { vos.VariantId, vos.VariantOptionId });
        });

        // Read model for cross-module integrity checks
        modelBuilder.Entity<OrderItemProductReference>(e =>
        {
            e.HasNoKey();
            e.ToTable("OrderItems", t => t.ExcludeFromMigrations());
            e.Property(x => x.ProductId).HasColumnName("ProductId");
        });
    }
}
