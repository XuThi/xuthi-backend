using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Behaviors;

/// <summary>
/// Pipeline behavior that wraps command handlers in a database transaction.
/// Skips transaction wrapping for requests that implement <see cref="ISkipTransaction"/>.
/// </summary>
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(
        IServiceProvider serviceProvider,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Skip transactions for requests that opt out
        if (request is ISkipTransaction)
        {
            return await next();
        }

        // Only wrap commands (not queries) — commands typically end with "Command"
        var requestName = typeof(TRequest).Name;
        if (!requestName.EndsWith("Command"))
        {
            return await next();
        }

        _logger.LogInformation("[Transaction] Starting transaction for {RequestName}", requestName);

        // Try to find a registered DbContext to use for the transaction
        var dbContextTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(DbContext)))
            .ToList();

        DbContext? dbContext = null;
        foreach (var dbType in dbContextTypes)
        {
            dbContext = _serviceProvider.GetService(dbType) as DbContext;
            if (dbContext != null) break;
        }

        if (dbContext is null)
        {
            _logger.LogWarning("[Transaction] No DbContext found for {RequestName}, proceeding without transaction", requestName);
            return await next();
        }

        if (dbContext.Database.CurrentTransaction is not null)
        {
            _logger.LogInformation("[Transaction] Reusing existing transaction for {RequestName}", requestName);
            return await next();
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var response = await next();
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("[Transaction] Committed transaction for {RequestName}", requestName);
            return response;
        });
    }
}

/// <summary>
/// Marker interface for commands that should NOT be wrapped in a transaction.
/// </summary>
public interface ISkipTransaction { }
