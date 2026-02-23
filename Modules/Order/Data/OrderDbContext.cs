using Core.DDD;
using Order.Orders.Models;

namespace Order.Data;

public class OrderDbContext(
    DbContextOptions<OrderDbContext> options,
    DispatchDomainEventsInterceptor? interceptor = null) : DbContext(options)
{
    public DbSet<CustomerOrder> Orders { get; set; } = default!;
    public DbSet<OrderItem> OrderItems { get; set; } = default!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (interceptor is not null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerOrder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.HasIndex(e => e.CustomerEmail);
            entity.HasIndex(e => e.Status);

            // Base class property mappings
            entity.Ignore(e => e.CreatedBy);
            entity.Ignore(e => e.UpdatedBy);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.UpdatedAt).HasColumnName("UpdatedAt");

            entity.HasMany(e => e.Items)
                .WithOne(e => e.Order)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderId);

            // Ignore base class audit properties not used by OrderItem
            entity.Ignore(e => e.CreatedAt);
            entity.Ignore(e => e.CreatedBy);
            entity.Ignore(e => e.UpdatedAt);
            entity.Ignore(e => e.UpdatedBy);
        });
    }
}
