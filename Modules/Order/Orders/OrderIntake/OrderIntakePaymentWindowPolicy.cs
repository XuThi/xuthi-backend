namespace Order.Orders.OrderIntake;

public sealed record OrderIntakePaymentWindowPolicy(
    TimeSpan PaymentWindow,
    TimeSpan PaymentSettlementGrace)
{
    public static OrderIntakePaymentWindowPolicy Default { get; } = new(
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(1));

    public DateTimeOffset GetPaymentWindowEnd(DateTimeOffset now)
        => now.Add(PaymentWindow);

    public DateTimeOffset GetSettlementGraceEnd(DateTimeOffset paymentWindowEnd)
        => paymentWindowEnd.Add(PaymentSettlementGrace);
}
