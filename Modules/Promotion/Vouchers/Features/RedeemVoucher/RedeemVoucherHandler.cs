namespace Promotion.Vouchers.Features.RedeemVoucher;

public record RedeemVoucherCommand(
    Guid VoucherId,
    Guid? CustomerId,
    Guid OrderId,
    decimal DiscountApplied) : ICommand<RedeemVoucherResult>;

public record RedeemVoucherResult(bool Success);

internal class RedeemVoucherHandler(PromotionDbContext db)
    : ICommandHandler<RedeemVoucherCommand, RedeemVoucherResult>
{
    public async Task<RedeemVoucherResult> Handle(RedeemVoucherCommand command, CancellationToken ct)
    {
        var existingUsage = await db.VoucherUsages
            .SingleOrDefaultAsync(u => u.OrderId == command.OrderId && u.VoucherId == command.VoucherId, ct);

        if (existingUsage is not null)
        {
            if (existingUsage.Status == VoucherUsageStatus.Released)
                throw new InvalidOperationException("Released voucher hold cannot be finalized");

            if (existingUsage.Status == VoucherUsageStatus.Held)
            {
                existingUsage.Status = VoucherUsageStatus.Finalized;
                existingUsage.FinalizedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            return new RedeemVoucherResult(true);
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
            Status = VoucherUsageStatus.Finalized,
            FinalizedAt = DateTime.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            var redeemedByRetry = await db.VoucherUsages
                .AsNoTracking()
                .AnyAsync(u => u.OrderId == command.OrderId
                    && u.VoucherId == command.VoucherId
                    && u.Status != VoucherUsageStatus.Released, ct);

            if (redeemedByRetry)
            {
                foreach (var entry in db.ChangeTracker
                    .Entries<VoucherUsage>()
                    .Where(e => e.Entity.OrderId == command.OrderId && e.Entity.VoucherId == command.VoucherId))
                {
                    entry.State = EntityState.Detached;
                }

                await db.Entry(voucher).ReloadAsync(ct);
                return new RedeemVoucherResult(true);
            }

            throw;
        }

        return new RedeemVoucherResult(true);
    }
}
