using Customer.Features.Customers;
using ProductCatalog.Infrastructure.Data;
using Promotion.Features.Vouchers;
using Promotion.Infrastructure.Data;

namespace Order.Features.Checkout;

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

            var itemTotal = variant.Price * item.Quantity;
            subtotal += itemTotal;

            // Get first image URL
            var imageUrl = product.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Image.Url;

            // Build variant description from option selections
            var variantDesc = string.Join(", ", variant.OptionSelections.Select(os => os.Value));

            orderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                ProductId = item.ProductId,
                VariantId = item.VariantId,
                ProductName = product.Name,
                VariantSku = variant.Sku,
                VariantDescription = variantDesc,
                ImageUrl = imageUrl,
                UnitPrice = variant.Price,
                CompareAtPrice = null, // Simplified - no compare price
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
            
            try
            {
                await sender.Send(new AddCustomerOrderCommand(
                    req.CustomerId.Value,
                    total,
                    pointsEarned,
                    order.Id
                ), cancellationToken);
            }
            catch
            {
                // Log but don't fail checkout if customer update fails
            }
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
}
