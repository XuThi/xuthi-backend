namespace Promotion.Vouchers.Features.ManageVoucherUsage;

public record HoldVoucherUsageCommand(
    Guid VoucherId,
    Guid? CustomerId,
    Guid OrderId,
    decimal DiscountApplied) : ICommand<VoucherUsageChangeResult>;

public record FinalizeVoucherUsageCommand(Guid VoucherId, Guid OrderId) : ICommand<VoucherUsageChangeResult>;

public record ReleaseVoucherUsageCommand(Guid VoucherId, Guid OrderId) : ICommand<VoucherUsageChangeResult>;

public record VoucherUsageChangeResult(bool Success);

public record GetVoucherUsageAuditQuery(Guid VoucherId) : IQuery<GetVoucherUsageAuditResult>;

public record GetVoucherUsageAuditResult(IReadOnlyList<VoucherUsageAuditEntry> Usages);

public record VoucherUsageAuditEntry(
    Guid VoucherId,
    Guid OrderId,
    Guid? CustomerId,
    decimal DiscountApplied,
    VoucherUsageStatus Status,
    DateTime HeldAt,
    DateTime? FinalizedAt,
    DateTime? ReleasedAt);

internal class HoldVoucherUsageHandler(PromotionDbContext db)
    : ICommandHandler<HoldVoucherUsageCommand, VoucherUsageChangeResult>
{
    public async Task<VoucherUsageChangeResult> Handle(HoldVoucherUsageCommand command, CancellationToken ct)
    {
        var existing = await db.VoucherUsages
            .SingleOrDefaultAsync(u => u.OrderId == command.OrderId && u.VoucherId == command.VoucherId, ct);

        if (existing is not null)
        {
            if (existing.Status == VoucherUsageStatus.Released)
                throw new InvalidOperationException("Voucher hold has already been released");

            return new VoucherUsageChangeResult(true);
        }

        var voucher = await db.Vouchers.FirstOrDefaultAsync(v => v.Id == command.VoucherId, ct)
            ?? throw new InvalidOperationException("Voucher not found");

        if (voucher.MaxUsageCount.HasValue && voucher.CurrentUsageCount >= voucher.MaxUsageCount)
            throw new InvalidOperationException("Mã giảm giá đã hết lượt sử dụng");

        voucher.CurrentUsageCount++;
        voucher.UpdatedAt = DateTime.UtcNow;

        db.VoucherUsages.Add(new VoucherUsage
        {
            Id = Guid.NewGuid(),
            VoucherId = command.VoucherId,
            CustomerId = command.CustomerId,
            OrderId = command.OrderId,
            DiscountApplied = command.DiscountApplied,
            UsedAt = DateTime.UtcNow,
            Status = VoucherUsageStatus.Held
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (await TryReturnExistingHoldAsync(command, voucher, ct))
                return new VoucherUsageChangeResult(true);

            await db.Entry(voucher).ReloadAsync(ct);

            if (voucher.MaxUsageCount.HasValue && voucher.CurrentUsageCount >= voucher.MaxUsageCount)
                throw new InvalidOperationException("Mã giảm giá đã hết lượt sử dụng");

            throw;
        }
        catch (DbUpdateException)
        {
            if (await TryReturnExistingHoldAsync(command, voucher, ct))
                return new VoucherUsageChangeResult(true);

            throw;
        }

        return new VoucherUsageChangeResult(true);
    }

    private async Task<bool> TryReturnExistingHoldAsync(
        HoldVoucherUsageCommand command,
        Voucher voucher,
        CancellationToken ct)
    {
        var heldByRetry = await db.VoucherUsages
            .AsNoTracking()
            .AnyAsync(u => u.OrderId == command.OrderId
                && u.VoucherId == command.VoucherId
                && u.Status != VoucherUsageStatus.Released, ct);

        if (!heldByRetry)
            return false;

        foreach (var entry in db.ChangeTracker
            .Entries<VoucherUsage>()
            .Where(e => e.Entity.OrderId == command.OrderId && e.Entity.VoucherId == command.VoucherId))
        {
            entry.State = EntityState.Detached;
        }

        await db.Entry(voucher).ReloadAsync(ct);
        return true;
    }
}

internal class FinalizeVoucherUsageHandler(PromotionDbContext db)
    : ICommandHandler<FinalizeVoucherUsageCommand, VoucherUsageChangeResult>
{
    public async Task<VoucherUsageChangeResult> Handle(FinalizeVoucherUsageCommand command, CancellationToken ct)
    {
        var usage = await db.VoucherUsages
            .SingleOrDefaultAsync(u => u.OrderId == command.OrderId && u.VoucherId == command.VoucherId, ct)
            ?? throw new InvalidOperationException("Voucher hold not found");

        if (usage.Status == VoucherUsageStatus.Released)
            throw new InvalidOperationException("Released voucher hold cannot be finalized");

        if (usage.Status == VoucherUsageStatus.Finalized)
            return new VoucherUsageChangeResult(true);

        usage.Status = VoucherUsageStatus.Finalized;
        usage.FinalizedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return new VoucherUsageChangeResult(true);
    }
}

internal class ReleaseVoucherUsageHandler(PromotionDbContext db)
    : ICommandHandler<ReleaseVoucherUsageCommand, VoucherUsageChangeResult>
{
    public async Task<VoucherUsageChangeResult> Handle(ReleaseVoucherUsageCommand command, CancellationToken ct)
    {
        var usage = await db.VoucherUsages
            .SingleOrDefaultAsync(u => u.OrderId == command.OrderId && u.VoucherId == command.VoucherId, ct)
            ?? throw new InvalidOperationException("Voucher hold not found");

        if (usage.Status == VoucherUsageStatus.Released)
            return new VoucherUsageChangeResult(true);

        var voucher = await db.Vouchers.FirstOrDefaultAsync(v => v.Id == command.VoucherId, ct)
            ?? throw new InvalidOperationException("Voucher not found");

        usage.Status = VoucherUsageStatus.Released;
        usage.ReleasedAt = DateTime.UtcNow;
        voucher.CurrentUsageCount = Math.Max(0, voucher.CurrentUsageCount - 1);
        voucher.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return new VoucherUsageChangeResult(true);
    }
}

internal class GetVoucherUsageAuditHandler(PromotionDbContext db)
    : IQueryHandler<GetVoucherUsageAuditQuery, GetVoucherUsageAuditResult>
{
    public async Task<GetVoucherUsageAuditResult> Handle(GetVoucherUsageAuditQuery query, CancellationToken ct)
    {
        var usages = await db.VoucherUsages
            .AsNoTracking()
            .Where(u => u.VoucherId == query.VoucherId)
            .OrderBy(u => u.UsedAt)
            .Select(u => new VoucherUsageAuditEntry(
                u.VoucherId,
                u.OrderId,
                u.CustomerId,
                u.DiscountApplied,
                u.Status,
                u.UsedAt,
                u.FinalizedAt,
                u.ReleasedAt))
            .ToListAsync(ct);

        return new GetVoucherUsageAuditResult(usages);
    }
}
