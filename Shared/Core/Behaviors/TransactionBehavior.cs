using System.Transactions;
using MediatR;

namespace Core.Behaviors;

/// <summary>
/// MediatR Pipeline Behavior that wraps command execution in a TransactionScope.
/// This ensures ACID compliance across multiple DbContexts in a modular monolith.
/// 
/// How it works:
/// 1. Before handler runs: Opens a TransactionScope
/// 2. Handler runs (can save to multiple DbContexts)
/// 3. Domain events are published (also within the transaction)
/// 4. If all succeeds: Transaction commits
/// 5. If anything throws: Transaction rolls back EVERYTHING
/// And yes this is from the official dotnet repositories
/// </summary>
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        // Only wrap Commands (not Queries) in transactions
        // Convention: Commands end with "Command", Queries end with "Query"
        if (!requestName.EndsWith("Command"))
        {
            return await next();
        }

        // TransactionScope options:
        // - Required: Join existing transaction or create new
        // - ReadCommitted: Good balance of consistency vs performance
        // - AsyncFlowOption: REQUIRED for async/await to work properly!
        var transactionOptions = new TransactionOptions
        {
            IsolationLevel = IsolationLevel.ReadCommitted,
            Timeout = TransactionManager.MaximumTimeout
        };

        using var scope = new TransactionScope(
            TransactionScopeOption.Required,
            transactionOptions,
            TransactionScopeAsyncFlowOption.Enabled); // Critical for async!

        try
        {
            var response = await next();

            // If we get here without exception, commit
            scope.Complete();

            return response;
        }
        catch
        {
            // TransactionScope automatically rolls back if Complete() not called
            throw;
        }
    }
}

/// <summary>
/// Marker interface for commands that should NOT be wrapped in a transaction.
/// Use for read-only operations that are named "Command" for some reason.
/// </summary>
public interface ISkipTransaction { }
