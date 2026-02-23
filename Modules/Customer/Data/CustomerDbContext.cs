using Core.DDD;

namespace Customer.Data;

public class CustomerDbContext : DbContext
{
    private readonly DispatchDomainEventsInterceptor? _interceptor;

    public CustomerDbContext(DbContextOptions<CustomerDbContext> options, DispatchDomainEventsInterceptor? interceptor = null) : base(options)
    {
        _interceptor = interceptor;
    }

    public DbSet<CustomerProfile> Customers => Set<CustomerProfile>();
    public DbSet<CustomerAddress> Addresses => Set<CustomerAddress>();
    public DbSet<PointsHistory> PointsHistory => Set<PointsHistory>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (_interceptor != null)
            optionsBuilder.AddInterceptors(_interceptor);

        base.OnConfiguring(optionsBuilder);
    }

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

            // Map base class properties
            entity.Property(c => c.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(c => c.UpdatedAt).HasColumnName("UpdatedAt");
            entity.Ignore(c => c.CreatedBy);
            entity.Ignore(c => c.UpdatedBy);
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

            // Ignore base class audit properties
            entity.Ignore(a => a.CreatedAt);
            entity.Ignore(a => a.CreatedBy);
            entity.Ignore(a => a.UpdatedAt);
            entity.Ignore(a => a.UpdatedBy);
        });

        modelBuilder.Entity<PointsHistory>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.CustomerId);
            entity.HasIndex(p => p.CreatedAt);
            entity.Property(p => p.Description).HasMaxLength(500).IsRequired();
            entity.Property(p => p.CreatedAt).HasColumnName("CreatedAt");

            // Ignore unused base class audit properties
            entity.Ignore(p => p.UpdatedAt);
            entity.Ignore(p => p.CreatedBy);
            entity.Ignore(p => p.UpdatedBy);
            
            entity.HasOne(p => p.Customer)
                .WithMany()
                .HasForeignKey(p => p.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        base.OnModelCreating(modelBuilder);
    }
}
