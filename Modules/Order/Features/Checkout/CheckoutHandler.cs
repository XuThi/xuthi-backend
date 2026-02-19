using Customer.Features.Customers;
using Customer.Features.Customers.AddCustomerOrder;
using ProductCatalog.Infrastructure.Data;
using Promotion.Features.Vouchers;
using Promotion.Features.Vouchers.ValidateVoucher;
using Promotion.Infrastructure.Data;

namespace Order.Features.Checkout;

public record CheckoutCommand(CheckoutRequest Request) : ICommand<CheckoutResult>;
public record CheckoutResult(Guid OrderId, string OrderNumber, decimal Total, string Status);
public record CheckoutItem(Guid ProductId, Guid VariantId, int Quantity);

public class CheckoutCommandValidator : AbstractValidator<CheckoutCommand>
{
    public CheckoutCommandValidator()
    {
        RuleFor(x => x.Request.CustomerName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.CustomerEmail).NotEmpty().EmailAddress();
        RuleFor(x => x.Request.CustomerPhone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Request.ShippingAddress).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Request.ShippingCity).NotEmpty();
        RuleFor(x => x.Request.ShippingDistrict).NotEmpty();
        RuleFor(x => x.Request.ShippingWard).NotEmpty();
        RuleFor(x => x.Request.Items).NotEmpty().WithMessage("Cart cannot be empty");
        RuleForEach(x => x.Request.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty();
            item.RuleFor(i => i.VariantId).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}

internal class CheckoutHandler(
    OrderDbContext orderDb,
    ProductCatalogDbContext catalogDb,
    PromotionDbContext promotionDb,
    ISender sender)
    : ICommandHandler<CheckoutCommand, CheckoutResult>
{
    public async Task<CheckoutResult> Handle(CheckoutCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;

        // 1. Validate and fetch variants
        var variantIds = req.Items.Select(i => i.VariantId).ToList();
        var variants = await catalogDb.Variants
            .Include(v => v.OptionSelections)
            .Where(v => variantIds.Contains(v.Id) && !v.IsDeleted)
            .ToListAsync(cancellationToken);

        // Get products for names
        var productIds = req.Items.Select(i => i.ProductId).ToList();
        var products = await catalogDb.Products
            .Include(p => p.Images)
                .ThenInclude(pi => pi.Image)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var optionIds = variants
            .SelectMany(v => v.OptionSelections.Select(os => os.VariantOptionId))
            .Distinct()
            .ToList();

        var optionNameMap = optionIds.Count == 0
            ? new Dictionary<string, string>()
            : await catalogDb.VariantOptions
                .Where(vo => optionIds.Contains(vo.Id))
                .ToDictionaryAsync(vo => vo.Id, vo => vo.Name, cancellationToken);

        // Validate all items exist
        var orderItems = new List<OrderItem>();
        decimal subtotal = 0;

        foreach (var item in req.Items)
        {
            var variant = variants.FirstOrDefault(v => v.Id == item.VariantId);
            if (variant is null)
                throw new InvalidOperationException($"Variant {item.VariantId} not found");

            var product = products.GetValueOrDefault(item.ProductId);
            if (product is null)
                throw new InvalidOperationException($"Product {item.ProductId} not found");

            var (unitPrice, compareAtPrice) = await ResolveSalePrice(
                promotionDb,
                product.Id,
                variant.Id,
                variant.Price,
                cancellationToken);

            var itemTotal = unitPrice * item.Quantity;
            subtotal += itemTotal;

            // Get first image URL
            var imageUrl = product.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Image.Url;

            // Build variant description from option selections
            var variantDesc = string.Join(", ", variant.OptionSelections.Select(os =>
            {
                var name = optionNameMap.TryGetValue(os.VariantOptionId, out var n) ? n : os.VariantOptionId;
                return $"{name}: {os.Value}";
            }));

            orderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                ProductId = item.ProductId,
                VariantId = item.VariantId,
                ProductName = product.Name,
                VariantSku = variant.Sku,
                VariantDescription = variantDesc,
                ImageUrl = imageUrl,
                UnitPrice = unitPrice,
                CompareAtPrice = compareAtPrice,
                Quantity = item.Quantity,
                TotalPrice = itemTotal
            });
        }

        // 2. Apply voucher via Promotion module
        decimal discountAmount = 0;
        Guid? voucherId = null;

        if (!string.IsNullOrWhiteSpace(req.VoucherCode))
        {
            var voucherResult = await sender.Send(new ValidateVoucherQuery(
                req.VoucherCode,
                subtotal,
                productIds,
                null, // CategoryId
                req.CustomerId,
                null // CustomerTier - could be fetched from Customer module
            ), cancellationToken);

            if (voucherResult.IsValid)
            {
                discountAmount = voucherResult.DiscountAmount;
                voucherId = voucherResult.VoucherId;

                // Increment voucher usage
                var voucher = await promotionDb.Vouchers.FindAsync([voucherId], cancellationToken);
                if (voucher != null)
                {
                    voucher.CurrentUsageCount++;
                    voucher.UpdatedAt = DateTime.UtcNow;
                }
            }
            else
            {
                throw new InvalidOperationException(voucherResult.ErrorMessage ?? "Invalid voucher");
            }
        }

        // 3. Calculate shipping (simplified - could be based on location)
        decimal shippingFee = 30000; // 30k VND flat rate, adjust as needed

        // 4. Create order
        var orderNumber = GenerateOrderNumber();
        var total = subtotal - discountAmount + shippingFee;

        var order = new CustomerOrder
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            CustomerId = req.CustomerId,
            CustomerName = req.CustomerName,
            CustomerEmail = req.CustomerEmail,
            CustomerPhone = req.CustomerPhone,
            ShippingAddress = req.ShippingAddress,
            ShippingCity = req.ShippingCity,
            ShippingDistrict = req.ShippingDistrict,
            ShippingWard = req.ShippingWard,
            ShippingNote = req.ShippingNote,
            Subtotal = subtotal,
            DiscountAmount = discountAmount,
            ShippingFee = shippingFee,
            Total = total,
            VoucherId = voucherId,
            VoucherCode = req.VoucherCode,
            PaymentMethod = req.PaymentMethod,
            Status = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending,
            Items = orderItems
        };

        // 5. Save order (no stock deduction in simplified design)
        orderDb.Orders.Add(order);
        await orderDb.SaveChangesAsync(cancellationToken);
        await promotionDb.SaveChangesAsync(cancellationToken);

        // 6. Update customer stats if logged in
        if (req.CustomerId.HasValue)
        {
            // Calculate points: 1 point per 10,000 VND spent
            var pointsEarned = (int)(total / 10000);

            await sender.Send(new AddCustomerOrderCommand(
                req.CustomerId.Value,
                total,
                pointsEarned,
                order.Id
            ), cancellationToken);
        }

        return new CheckoutResult(
            order.Id,
            order.OrderNumber,
            order.Total,
            order.Status.ToString()
        );
    }

    private static string GenerateOrderNumber()
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = new Random().Next(1000, 9999);
        return $"XT-{date}-{random}";
    }

    private static async Task<(decimal UnitPrice, decimal? CompareAtPrice)> ResolveSalePrice(
        PromotionDbContext promotionDb,
        Guid productId,
        Guid variantId,
        decimal basePrice,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var saleItem = await promotionDb.SaleCampaignItems
            .Include(i => i.SaleCampaign)
            .Where(i => i.ProductId == productId && (i.VariantId == null || i.VariantId == variantId))
            .Where(i => i.SaleCampaign.IsActive && i.SaleCampaign.StartDate <= now && i.SaleCampaign.EndDate >= now)
            .OrderByDescending(i => i.VariantId.HasValue)
            .ThenBy(i => i.SalePrice)
            .FirstOrDefaultAsync(ct);

        if (saleItem is null)
        {
            return (basePrice, null);
        }

        var original = saleItem.OriginalPrice ?? basePrice;
        if (original < saleItem.SalePrice)
        {
            original = basePrice;
        }

        return (saleItem.SalePrice, original);
    }
}
