using Promotion.Vouchers.Features.ValidateVoucher;
using Promotion.Vouchers.Models;

namespace XuThiWebApp.Tests;

public sealed class VoucherDiscountBaseTests
{
    [Fact]
    public async Task Product_scoped_percentage_uses_only_matching_discount_base_lines()
    {
        await using var app = new CommerceTestApp();
        var eligibleProductId = Guid.NewGuid();
        var otherProductId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        await app.SeedVoucherAsync(
            code: "HALF-PRODUCT",
            type: VoucherType.Percentage,
            discountValue: 50m,
            applicableProductIds: [eligibleProductId]);

        var result = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "half-product",
            CartQuoteAmount: 300_000m,
            Lines:
            [
                new VoucherValidationLine(eligibleProductId, categoryId, 100_000m, IsOnSale: false),
                new VoucherValidationLine(otherProductId, categoryId, 200_000m, IsOnSale: false)
            ]));

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal(50_000m, result.DiscountAmount);
    }

    [Fact]
    public async Task Category_scoped_fixed_discount_is_capped_by_discount_base()
    {
        await using var app = new CommerceTestApp();
        var eligibleCategoryId = Guid.NewGuid();
        var otherCategoryId = Guid.NewGuid();

        await app.SeedVoucherAsync(
            code: "CATEGORY-FIXED",
            type: VoucherType.FixedAmount,
            discountValue: 250_000m,
            applicableCategoryId: eligibleCategoryId);

        var result = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "category-fixed",
            CartQuoteAmount: 520_000m,
            Lines:
            [
                new VoucherValidationLine(Guid.NewGuid(), eligibleCategoryId, 120_000m, IsOnSale: false),
                new VoucherValidationLine(Guid.NewGuid(), otherCategoryId, 400_000m, IsOnSale: false)
            ]));

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal(120_000m, result.DiscountAmount);
    }

    [Fact]
    public async Task Sale_combination_rule_controls_whether_sale_lines_enter_discount_base()
    {
        await using var app = new CommerceTestApp();
        var eligibleCategoryId = Guid.NewGuid();
        var otherCategoryId = Guid.NewGuid();

        await app.SeedVoucherAsync(
            code: "REGULAR10",
            type: VoucherType.Percentage,
            discountValue: 10m,
            applicableCategoryId: eligibleCategoryId,
            configure: voucher => voucher.CanCombineWithSalePrice = false);
        await app.SeedVoucherAsync(
            code: "ANY10",
            type: VoucherType.Percentage,
            discountValue: 10m,
            applicableCategoryId: eligibleCategoryId,
            configure: voucher => voucher.CanCombineWithSalePrice = true);

        var lines = new List<VoucherValidationLine>
        {
            new(Guid.NewGuid(), eligibleCategoryId, 100_000m, IsOnSale: true),
            new(Guid.NewGuid(), eligibleCategoryId, 200_000m, IsOnSale: false),
            new(Guid.NewGuid(), otherCategoryId, 700_000m, IsOnSale: false)
        };

        var nonCombinable = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "regular10",
            CartQuoteAmount: 1_000_000m,
            Lines: lines));
        var combinable = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "any10",
            CartQuoteAmount: 1_000_000m,
            Lines: lines));

        Assert.True(nonCombinable.IsValid, nonCombinable.ErrorMessage);
        Assert.Equal(20_000m, nonCombinable.DiscountAmount);
        Assert.True(combinable.IsValid, combinable.ErrorMessage);
        Assert.Equal(30_000m, combinable.DiscountAmount);
    }

    [Fact]
    public async Task Free_shipping_voucher_is_valid_without_item_discount()
    {
        await using var app = new CommerceTestApp();
        await app.SeedVoucherAsync(
            code: "SHIPFREE",
            type: VoucherType.FreeShipping,
            discountValue: 40_000m);

        var result = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "shipfree",
            CartQuoteAmount: 300_000m,
            Lines:
            [
                new VoucherValidationLine(
                    ProductId: Guid.NewGuid(),
                    CategoryId: Guid.NewGuid(),
                    LineTotal: 300_000m,
                    IsOnSale: false)
            ]));

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal(VoucherType.FreeShipping, result.Type);
        Assert.Equal(0m, result.DiscountAmount);
    }

    [Fact]
    public async Task Customer_tier_rule_requires_customer_promotion_facts()
    {
        await using var app = new CommerceTestApp();
        await app.SeedVoucherAsync(
            code: "TIER20",
            type: VoucherType.Percentage,
            discountValue: 20m,
            configure: voucher => voucher.MinimumCustomerTier = 2);

        var customerId = Guid.NewGuid();
        var missingTier = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "tier20",
            CartQuoteAmount: 100_000m,
            CustomerId: customerId,
            CustomerTier: null));
        var lowTier = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "tier20",
            CartQuoteAmount: 100_000m,
            CustomerId: customerId,
            CustomerTier: 1));
        var eligibleTier = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "tier20",
            CartQuoteAmount: 100_000m,
            CustomerId: customerId,
            CustomerTier: 2));

        Assert.False(missingTier.IsValid);
        Assert.False(lowTier.IsValid);
        Assert.True(eligibleTier.IsValid, eligibleTier.ErrorMessage);
        Assert.Equal(20_000m, eligibleTier.DiscountAmount);
    }

    [Fact]
    public async Task First_purchase_rule_requires_customer_order_count_facts()
    {
        await using var app = new CommerceTestApp();
        await app.SeedVoucherAsync(
            code: "FIRST10",
            type: VoucherType.FixedAmount,
            discountValue: 10_000m,
            configure: voucher => voucher.FirstPurchaseOnly = true);

        var customerId = Guid.NewGuid();
        var missingOrderCount = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "first10",
            CartQuoteAmount: 100_000m,
            CustomerId: customerId,
            CustomerTotalOrders: null));
        var returningCustomer = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "first10",
            CartQuoteAmount: 100_000m,
            CustomerId: customerId,
            CustomerTotalOrders: 1));
        var firstPurchase = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "first10",
            CartQuoteAmount: 100_000m,
            CustomerId: customerId,
            CustomerTotalOrders: 0));

        Assert.False(missingOrderCount.IsValid);
        Assert.False(returningCustomer.IsValid);
        Assert.True(firstPurchase.IsValid, firstPurchase.ErrorMessage);
        Assert.Equal(10_000m, firstPurchase.DiscountAmount);
    }

    [Fact]
    public async Task Per_customer_usage_limit_requires_customer_identity()
    {
        await using var app = new CommerceTestApp();
        await app.SeedVoucherAsync(
            code: "ONE-EACH",
            type: VoucherType.FixedAmount,
            discountValue: 10_000m,
            configure: voucher => voucher.MaxUsagePerCustomer = 1);

        var missingCustomer = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "one-each",
            CartQuoteAmount: 100_000m));
        var identifiedCustomer = await app.Sender.Send(new ValidateVoucherQuery(
            Code: "one-each",
            CartQuoteAmount: 100_000m,
            CustomerId: Guid.NewGuid()));

        Assert.False(missingCustomer.IsValid);
        Assert.Contains("đăng nhập", missingCustomer.ErrorMessage);
        Assert.True(identifiedCustomer.IsValid, identifiedCustomer.ErrorMessage);
        Assert.Equal(10_000m, identifiedCustomer.DiscountAmount);
    }
}
