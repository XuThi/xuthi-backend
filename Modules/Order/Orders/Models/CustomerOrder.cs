using Core.DDD;
using Contracts;

namespace Order.Orders.Models;

public class CustomerOrder : Aggregate<Guid>
{
    public string OrderNumber { get; set; } = default!; // e.g., "XT-20260131-001"
    public Guid? SourceCartId { get; set; }
    
    // Customer reference in the Customer module
    public Guid CustomerId { get; set; }
    
    // Customer info (required for all orders)
    public string CustomerName { get; set; } = default!;
    public string CustomerEmail { get; set; } = default!;
    public string CustomerPhone { get; set; } = default!;
    
    // Shipping address
    public string ShippingAddress { get; set; } = default!;
    public string ShippingCity { get; set; } = default!;
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
    public DateTime? CreatedOrderAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public string? CancellationReason { get; set; }
    
    // Payment
    public long? PayOsOrderCode { get; set; } // PayOS order code for online payments
    public string? PaymentLinkId { get; set; } // PayOS payment link ID
    public string? PaymentLinkUrl { get; set; }
    public DateTime? PaymentWindowExpiresAt { get; set; }
    public DateTime? PaymentSettlementGraceEndsAt { get; set; }
    
    // Stock reservation session key
    public string? ReservationSessionKey { get; set; }
    
    // Navigation
    public List<OrderItem> Items { get; set; } = [];

    public void ChangeStatus(OrderStatus newStatus, DateTime occurredAt, string? reason = null)
    {
        ValidateStatusTransition(Status, newStatus);

        CustomerOrderOutcome? outcome = null;
        switch (newStatus)
        {
            case OrderStatus.Confirmed:
                break;
            case OrderStatus.Processing:
                break;
            case OrderStatus.Shipped:
                ShippedAt = occurredAt;
                break;
            case OrderStatus.Delivered:
                DeliveredAt = occurredAt;
                if (PaymentMethod == PaymentMethod.CashOnDelivery)
                {
                    PaymentStatus = PaymentStatus.Paid;
                    PaidAt = occurredAt;
                }
                outcome = CustomerOrderOutcome.Delivered;
                break;
            case OrderStatus.Cancelled:
                CancelledAt = occurredAt;
                CancellationReason = reason;
                outcome = CustomerOrderOutcome.Cancelled;
                break;
            case OrderStatus.Returned:
                ReturnedAt = occurredAt;
                outcome = CustomerOrderOutcome.Returned;
                break;
            default:
                throw new InvalidOperationException($"Cannot transition from {Status} to {newStatus}");
        }

        Status = newStatus;
        UpdatedAt = occurredAt;

        if (outcome is not null)
            RaiseCustomerOrderOutcome(outcome.Value, occurredAt);
    }

    private static void ValidateStatusTransition(OrderStatus current, OrderStatus target)
    {
        var validTransitions = new Dictionary<OrderStatus, OrderStatus[]>
        {
            [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
            [OrderStatus.Confirmed] = [OrderStatus.Processing, OrderStatus.Cancelled],
            [OrderStatus.Processing] = [OrderStatus.Shipped, OrderStatus.Cancelled],
            [OrderStatus.Shipped] = [OrderStatus.Delivered, OrderStatus.Returned],
            [OrderStatus.Delivered] = [OrderStatus.Returned],
            [OrderStatus.Cancelled] = [],
            [OrderStatus.Returned] = []
        };

        if (!validTransitions.TryGetValue(current, out var allowed) || !allowed.Contains(target))
            throw new InvalidOperationException($"Cannot transition from {current} to {target}");
    }

    private void RaiseCustomerOrderOutcome(CustomerOrderOutcome outcome, DateTime occurredAt)
    {
        if (CustomerId == Guid.Empty)
            throw new InvalidOperationException("Persisted Orders must have a CustomerId before Customer Order Outcomes can be emitted.");

        ValidateOutcomeMoneyFacts();

        AddDomainEvent(new CustomerOrderOutcomeOccurred(
            CustomerId,
            Id,
            OrderNumber,
            outcome,
            occurredAt,
            Subtotal,
            DiscountAmount,
            ShippingFee,
            Total));
    }

    private void ValidateOutcomeMoneyFacts()
    {
        if (Subtotal < 0 || DiscountAmount < 0 || ShippingFee < 0 || Total < 0)
            throw new InvalidOperationException("Customer Order Outcome money values must not be negative.");

        if (DiscountAmount > Subtotal)
            throw new InvalidOperationException("Customer Order Outcome discount cannot exceed subtotal.");

        if (Total != Subtotal - DiscountAmount + ShippingFee)
            throw new InvalidOperationException("Customer Order Outcome total must equal subtotal minus discount plus shipping fee.");
    }
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
    PayOS = 3             // PayOS online payment
}
