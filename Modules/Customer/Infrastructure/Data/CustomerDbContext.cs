using Customer.Infrastructure.Entity;

namespace Customer.Infrastructure.Data;

public class CustomerDbContext : DbContext
{
    public CustomerDbContext(DbContextOptions<CustomerDbContext> options) : base(options)
    {
    }

    public DbSet<CustomerProfile> Customers => Set<CustomerProfile>();
    public DbSet<CustomerAddress> Addresses => Set<CustomerAddress>();
    public DbSet<PointsHistory> PointsHistory => Set<PointsHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerProfile>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.ExternalUserId).IsUnique();
            entity.HasIndex(c => c.Email);
            entity.Property(c => c.ExternalUserId).HasMaxLength(100).IsRequired();
            entity.Property(c => c.Email).HasMaxLength(256).IsRequired();
            entity.Property(c => c.FullName).HasMaxLength(200);
            entity.Property(c => c.Phone).HasMaxLength(20);
            entity.Property(c => c.TotalSpent).HasPrecision(18, 2);
            
            entity.HasMany(c => c.Addresses)
                .WithOne(a => a.Customer)
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ignore computed property
            entity.Ignore(c => c.TierDiscountPercentage);
        });

        modelBuilder.Entity<CustomerAddress>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasIndex(a => new { a.CustomerId, a.IsDefault });
            entity.Property(a => a.Label).HasMaxLength(50).IsRequired();
            entity.Property(a => a.RecipientName).HasMaxLength(200).IsRequired();
            entity.Property(a => a.Phone).HasMaxLength(20).IsRequired();
            entity.Property(a => a.Address).HasMaxLength(500).IsRequired();
            entity.Property(a => a.Ward).HasMaxLength(100).IsRequired();
            entity.Property(a => a.District).HasMaxLength(100).IsRequired();
            entity.Property(a => a.City).HasMaxLength(100).IsRequired();
            entity.Property(a => a.Note).HasMaxLength(500);
        });

        modelBuilder.Entity<PointsHistory>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.CustomerId);
            entity.HasIndex(p => p.CreatedAt);
            entity.Property(p => p.Description).HasMaxLength(500).IsRequired();
            
            entity.HasOne(p => p.Customer)
                .WithMany()
                .HasForeignKey(p => p.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}
