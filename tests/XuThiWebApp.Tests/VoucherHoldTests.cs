using Promotion.Vouchers.Features.ManageVoucherUsage;
using Promotion.Vouchers.Features.ValidateVoucher;
using Promotion.Vouchers.Models;

namespace XuThiWebApp.Tests;

public sealed class VoucherHoldTests
{
    [Fact]
    public async Task Held_voucher_usage_counts_against_capacity_until_released()
    {
        await using var app = new CommerceTestApp();
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await app.SeedVoucherAsync(
            code: "HOLD10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var voucherId = await app.GetVoucherIdAsync("HOLD10");

        await app.Sender.Send(new HoldVoucherUsageCommand(voucherId, customerId, orderId, 10m));

        var heldCapacity = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "HOLD10",
            CartQuoteAmount: 100m,
            CustomerId: customerId));

        Assert.False(heldCapacity.IsValid);
        Assert.Contains("hết lượt", heldCapacity.ErrorMessage);

        await app.Sender.Send(new ReleaseVoucherUsageCommand(voucherId, orderId));

        var releasedCapacity = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "HOLD10",
            CartQuoteAmount: 100m,
            CustomerId: customerId));

        Assert.True(releasedCapacity.IsValid, releasedCapacity.ErrorMessage);

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var usage = Assert.Single(audit.Usages);

        Assert.Equal(orderId, usage.OrderId);
        Assert.Equal(VoucherUsageStatus.Released, usage.Status);
        Assert.NotNull(usage.ReleasedAt);
    }

    [Fact]
    public async Task Finalized_voucher_usage_counts_against_capacity_and_records_created_order()
    {
        await using var app = new CommerceTestApp();
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await app.SeedVoucherAsync(
            code: "FINAL10",
            type: VoucherType.FixedAmount,
            discountValue: 10m,
            maxUsageCount: 1);

        var voucherId = await app.GetVoucherIdAsync("FINAL10");

        await app.Sender.Send(new HoldVoucherUsageCommand(voucherId, customerId, orderId, 10m));
        await app.Sender.Send(new FinalizeVoucherUsageCommand(voucherId, orderId));

        var capacity = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "FINAL10",
            CartQuoteAmount: 100m,
            CustomerId: customerId));

        Assert.False(capacity.IsValid);
        Assert.Contains("hết lượt", capacity.ErrorMessage);

        var audit = await app.Sender.Send(new GetVoucherUsageAuditQuery(voucherId));
        var usage = Assert.Single(audit.Usages);

        Assert.Equal(orderId, usage.OrderId);
        Assert.Equal(VoucherUsageStatus.Finalized, usage.Status);
        Assert.NotNull(usage.FinalizedAt);
        Assert.Null(usage.ReleasedAt);
    }
}
