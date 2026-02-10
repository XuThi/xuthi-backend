using MediatR;

namespace Core.Behaviors;

// TODO: Enable this later
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {

        return await next();
    }
}

/// <summary>
/// Marker interface for commands that should NOT be wrapped in a transaction.
/// Use for read-only operations that are named "Command" for some reason.
/// </summary>
public interface ISkipTransaction { }
