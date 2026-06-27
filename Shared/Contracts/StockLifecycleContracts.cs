using Contracts.CQRS;
using Core.Behaviors;

namespace Contracts;

public record HoldOrderAttemptStockCommand(
    Guid OrderId,
    IReadOnlyList<StockLifecycleLine> Lines,
    DateTime HoldExpiresAt) : ICommand<StockLifecycleResult>, ISkipTransaction;

public record CommitOrderStockCommand(
    Guid OrderId,
    StockLifecycleExpectedPriorState ExpectedPriorState,
    IReadOnlyList<StockLifecycleLine> Lines) : ICommand<StockLifecycleResult>, ISkipTransaction;

public record ReleaseOrderAttemptStockCommand(Guid OrderId)
    : ICommand<StockLifecycleResult>, ISkipTransaction;

public record StockLifecycleLine(Guid ProductVariantId, int Quantity);

public enum StockLifecycleExpectedPriorState
{
    None,
    Held
}

public record StockLifecycleResult(
    StockLifecycleResultStatus Status,
    IReadOnlyList<StockLifecycleLine> Lines,
    IReadOnlyList<StockLifecycleInsufficientStockDetail> InsufficientStockDetails,
    IReadOnlyList<StockLifecycleValidationDetail> ValidationDetails,
    StockLifecycleConflictDetail? Conflict)
{
    public bool IsSuccess => Status == StockLifecycleResultStatus.Succeeded;

    public static StockLifecycleResult Succeeded(IReadOnlyList<StockLifecycleLine> lines)
        => new(
            StockLifecycleResultStatus.Succeeded,
            lines,
            [],
            [],
            null);

    public static StockLifecycleResult ValidationFailed(
        IReadOnlyList<StockLifecycleValidationDetail> validationDetails)
        => new(
            StockLifecycleResultStatus.ValidationFailed,
            [],
            [],
            validationDetails,
            null);

    public static StockLifecycleResult InsufficientStock(
        IReadOnlyList<StockLifecycleLine> lines,
        IReadOnlyList<StockLifecycleInsufficientStockDetail> insufficientStockDetails)
        => new(
            StockLifecycleResultStatus.InsufficientStock,
            lines,
            insufficientStockDetails,
            [],
            null);

    public static StockLifecycleResult Conflicted(
        IReadOnlyList<StockLifecycleLine> lines,
        StockLifecycleConflictDetail conflict)
        => new(
            StockLifecycleResultStatus.Conflict,
            lines,
            [],
            [],
            conflict);
}

public enum StockLifecycleResultStatus
{
    Succeeded,
    ValidationFailed,
    InsufficientStock,
    Conflict
}

public record StockLifecycleInsufficientStockDetail(
    Guid ProductVariantId,
    int RequestedQuantity,
    int AvailableQuantity);

public record StockLifecycleValidationDetail(
    Guid? ProductVariantId,
    string Code,
    string Message);

public record StockLifecycleConflictDetail(
    string Reason,
    string ExpectedState,
    string ExistingState,
    IReadOnlyList<StockLifecycleLine> ExpectedLines,
    IReadOnlyList<StockLifecycleLine> ExistingLines,
    DateTime? ExpectedHoldExpiresAt,
    DateTime? ExistingHoldExpiresAt);

public record OrderStockHeld(
    Guid OrderId,
    IReadOnlyList<StockLifecycleLine> Lines,
    DateTime OccurredAt,
    string IdempotencyKey);

public record OrderStockCommitted(
    Guid OrderId,
    IReadOnlyList<StockLifecycleLine> Lines,
    DateTime OccurredAt,
    string IdempotencyKey);

public record OrderStockHoldReleased(
    Guid OrderId,
    IReadOnlyList<StockLifecycleLine> Lines,
    DateTime OccurredAt,
    string IdempotencyKey);
