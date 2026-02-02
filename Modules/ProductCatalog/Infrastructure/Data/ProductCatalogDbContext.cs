using Microsoft.EntityFrameworkCore;

namespace ProductCatalog.Infrastructure.Data;

public class ProductCatalogDbContext(DbContextOptions<ProductCatalogDbContext> options) : DbContext(options)
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
        });

        // Category
        modelBuilder.Entity<Category>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.UrlSlug).IsUnique();
        });

        // Group
        modelBuilder.Entity<Group>(e =>
        {
            e.HasKey(g => g.Id);
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
        });

        // Image
        modelBuilder.Entity<Image>(e =>
        {
            e.HasKey(i => i.Id);
        });

        // ProductImage
        modelBuilder.Entity<ProductImage>(e =>
        {
            e.HasKey(pi => pi.Id);
            e.HasOne(pi => pi.Image).WithMany().HasForeignKey(pi => pi.ImageId);
        });

        // Variant
        modelBuilder.Entity<Variant>(e =>
        {
            e.HasKey(v => v.Id);
            e.HasIndex(v => v.Sku).IsUnique();
            e.HasMany(v => v.OptionSelections).WithOne().HasForeignKey(os => os.VariantId);
            e.HasMany(v => v.Images).WithOne().HasForeignKey(vi => vi.VariantId);
        });

        // VariantImage
        modelBuilder.Entity<VariantImage>(e =>
        {
            e.HasKey(vi => vi.Id);
            e.HasOne(vi => vi.Image).WithMany().HasForeignKey(vi => vi.ImageId);
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
    }
}