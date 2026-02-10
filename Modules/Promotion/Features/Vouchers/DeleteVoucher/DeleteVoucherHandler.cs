using Promotion.Infrastructure.Data;

namespace Promotion.Features.Vouchers.DeleteVoucher;

// Command and Result
public record DeleteVoucherCommand(Guid Id) : ICommand<DeleteVoucherResult>;
public record DeleteVoucherResult(bool Success);

// Validator
public class DeleteVoucherCommandValidator : AbstractValidator<DeleteVoucherCommand>
{
    public DeleteVoucherCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required");
    }
}

// Handler
internal class DeleteVoucherHandler(PromotionDbContext db)
    : ICommandHandler<DeleteVoucherCommand, DeleteVoucherResult>
{
    public async Task<DeleteVoucherResult> Handle(DeleteVoucherCommand cmd, CancellationToken ct)
    {
        var voucher = await db.Vouchers.FindAsync([cmd.Id], ct);
        if (voucher is null)
            return new DeleteVoucherResult(false);

        db.Vouchers.Remove(voucher);
        await db.SaveChangesAsync(ct);
        return new DeleteVoucherResult(true);
    }
}
