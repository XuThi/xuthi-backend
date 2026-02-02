using Customer.Infrastructure.Data;
using Customer.Infrastructure.Entity;

namespace Customer.Features.Customers;

// Get or create customer (called after Keycloak login)
internal class GetOrCreateCustomerHandler(CustomerDbContext db)
    : IQueryHandler<GetOrCreateCustomerQuery, GetOrCreateCustomerResult>
{
    public async Task<GetOrCreateCustomerResult> Handle(GetOrCreateCustomerQuery query, CancellationToken ct)
    {
        var customer = await db.Customers
            .FirstOrDefaultAsync(c => c.KeycloakUserId == query.KeycloakUserId, ct);

        if (customer is null)
        {
            customer = new CustomerProfile
            {
                Id = Guid.NewGuid(),
                KeycloakUserId = query.KeycloakUserId,
                Email = query.Email,
                FullName = query.FullName,
                Tier = CustomerTier.Standard
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync(ct);

            return new GetOrCreateCustomerResult(MapToDto(customer), true);
        }

        // Update last login
        customer.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new GetOrCreateCustomerResult(MapToDto(customer), false);
    }

    private static CustomerDto MapToDto(CustomerProfile c) => new(
        c.Id, c.KeycloakUserId, c.Email, c.FullName, c.Phone,
        c.Tier, c.LoyaltyPoints, c.TotalSpent, c.TotalOrders,
        c.TierDiscountPercentage, c.CreatedAt, c.LastOrderAt);
}

// Get customer by ID
internal class GetCustomerHandler(CustomerDbContext db)
    : IQueryHandler<GetCustomerQuery, GetCustomerResult>
{
    public async Task<GetCustomerResult> Handle(GetCustomerQuery query, CancellationToken ct)
    {
        var customer = await db.Customers
            .Include(c => c.Addresses)
            .FirstOrDefaultAsync(c => c.Id == query.Id, ct);

        return customer is null 
            ? new GetCustomerResult(null) 
            : new GetCustomerResult(MapToDetailDto(customer));
    }

    private static CustomerDetailDto MapToDetailDto(CustomerProfile c) => new(
        c.Id, c.KeycloakUserId, c.Email, c.FullName, c.Phone,
        c.DateOfBirth, c.Gender, c.Tier, c.LoyaltyPoints, c.TotalSpent, c.TotalOrders,
        c.TierDiscountPercentage, c.AcceptsMarketing, c.AcceptsSms,
        c.CreatedAt, c.LastLoginAt, c.LastOrderAt,
        c.Addresses.Select(a => new CustomerAddressDto(
            a.Id, a.Label, a.RecipientName, a.Phone, a.Address,
            a.Ward, a.District, a.City, a.Note, a.IsDefault
        )).ToList());
}

// Get customer by Keycloak ID
internal class GetCustomerByKeycloakIdHandler(CustomerDbContext db)
    : IQueryHandler<GetCustomerByKeycloakIdQuery, GetCustomerByKeycloakIdResult>
{
    public async Task<GetCustomerByKeycloakIdResult> Handle(GetCustomerByKeycloakIdQuery query, CancellationToken ct)
    {
        var customer = await db.Customers
            .Include(c => c.Addresses)
            .FirstOrDefaultAsync(c => c.KeycloakUserId == query.KeycloakUserId, ct);

        return customer is null 
            ? new GetCustomerByKeycloakIdResult(null) 
            : new GetCustomerByKeycloakIdResult(MapToDetailDto(customer));
    }

    private static CustomerDetailDto MapToDetailDto(CustomerProfile c) => new(
        c.Id, c.KeycloakUserId, c.Email, c.FullName, c.Phone,
        c.DateOfBirth, c.Gender, c.Tier, c.LoyaltyPoints, c.TotalSpent, c.TotalOrders,
        c.TierDiscountPercentage, c.AcceptsMarketing, c.AcceptsSms,
        c.CreatedAt, c.LastLoginAt, c.LastOrderAt,
        c.Addresses.Select(a => new CustomerAddressDto(
            a.Id, a.Label, a.RecipientName, a.Phone, a.Address,
            a.Ward, a.District, a.City, a.Note, a.IsDefault
        )).ToList());
}

// Update customer profile
internal class UpdateCustomerHandler(CustomerDbContext db)
    : ICommandHandler<UpdateCustomerCommand, UpdateCustomerResult>
{
    public async Task<UpdateCustomerResult> Handle(UpdateCustomerCommand cmd, CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([cmd.Id], ct);
        if (customer is null)
            return new UpdateCustomerResult(false);

        if (cmd.FullName is not null) customer.FullName = cmd.FullName;
        if (cmd.Phone is not null) customer.Phone = cmd.Phone;
        if (cmd.DateOfBirth.HasValue) customer.DateOfBirth = cmd.DateOfBirth;
        if (cmd.Gender.HasValue) customer.Gender = cmd.Gender;
        if (cmd.AcceptsMarketing.HasValue) customer.AcceptsMarketing = cmd.AcceptsMarketing.Value;
        if (cmd.AcceptsSms.HasValue) customer.AcceptsSms = cmd.AcceptsSms.Value;
        
        customer.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new UpdateCustomerResult(true);
    }
}

// Add order stats to customer (called from Order module after successful order)
internal class AddCustomerOrderHandler(CustomerDbContext db)
    : ICommandHandler<AddCustomerOrderCommand, AddCustomerOrderResult>
{
    // Tier thresholds in VND
    private const decimal SilverThreshold = 1_000_000;
    private const decimal GoldThreshold = 5_000_000;
    private const decimal PlatinumThreshold = 10_000_000;

    public async Task<AddCustomerOrderResult> Handle(AddCustomerOrderCommand cmd, CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([cmd.CustomerId], ct);
        if (customer is null)
            throw new InvalidOperationException($"Customer {cmd.CustomerId} not found");

        // Update stats
        customer.TotalSpent += cmd.OrderTotal;
        customer.TotalOrders++;
        customer.LoyaltyPoints += cmd.PointsEarned;
        customer.LastOrderAt = DateTime.UtcNow;
        customer.UpdatedAt = DateTime.UtcNow;

        // Calculate new tier
        var newTier = customer.TotalSpent switch
        {
            >= PlatinumThreshold => CustomerTier.Platinum,
            >= GoldThreshold => CustomerTier.Gold,
            >= SilverThreshold => CustomerTier.Silver,
            _ => CustomerTier.Standard
        };

        if (newTier > customer.Tier)
            customer.Tier = newTier;

        // Record points earned
        db.PointsHistory.Add(new PointsHistory
        {
            Id = Guid.NewGuid(),
            CustomerId = cmd.CustomerId,
            Type = PointsTransactionType.Earned,
            Points = cmd.PointsEarned,
            BalanceAfter = customer.LoyaltyPoints,
            Description = $"Order completed",
            RelatedOrderId = cmd.OrderId
        });

        await db.SaveChangesAsync(ct);

        return new AddCustomerOrderResult(customer.Tier, customer.LoyaltyPoints);
    }
}
