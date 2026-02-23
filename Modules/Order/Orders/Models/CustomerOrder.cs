using Core.DDD;

namespace Order.Orders.Models;

public class CustomerOrder : Aggregate<Guid>
{
    public string OrderNumber { get; set; } = default!; // e.g., "XT-20260131-001"
    
    // Customer reference (optional - for logged in users)
    public Guid? CustomerId { get; set; } // Links to Customer module
    
    // Customer info (required for all orders)
    public string CustomerName { get; set; } = default!;
    public string CustomerEmail { get; set; } = default!;
    public string CustomerPhone { get; set; } = default!;
    
    // Shipping address
    public string ShippingAddress { get; set; } = default!;
    public string ShippingCity { get; set; } = default!;
    public string ShippingDistrict { get; set; } = default!;
    public string ShippingWard { get; set; } = default!;
    public string? ShippingNote { get; set; } // Delivery instructions
    
    // Order totals
    public decimal Subtotal { get; set; } // Before discount
    public decimal DiscountAmount { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Total { get; set; } // Final amount
    
    // Voucher info (if applied)
    public Guid? VoucherId { get; set; }
    public string? VoucherCode { get; set; }
    
    // Status
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public PaymentMethod PaymentMethod { get; set; }
    
    // Timestamps
    public DateTime? PaidAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    
    // Navigation
    public List<OrderItem> Items { get; set; } = [];
}

public enum OrderStatus
{
    Pending = 1,      // Order placed, waiting for confirmation
    Confirmed = 2,    // Order confirmed by admin
    Processing = 3,   // Being prepared
    Shipped = 4,      // Handed to courier
    Delivered = 5,    // Customer received
    Cancelled = 6,    // Cancelled
    Returned = 7      // Returned
}

public enum PaymentStatus
{
    Pending = 1,      // Awaiting payment
    Paid = 2,         // Payment received
    Failed = 3,       // Payment failed
    Refunded = 4      // Refunded
}

public enum PaymentMethod
{
    CashOnDelivery = 1,   // COD - common in Vietnam
    BankTransfer = 2,     // Manual bank transfer
    MoMo = 3,             // MoMo wallet
    ZaloPay = 4,          // ZaloPay
    VNPay = 5             // VNPay gateway
}
