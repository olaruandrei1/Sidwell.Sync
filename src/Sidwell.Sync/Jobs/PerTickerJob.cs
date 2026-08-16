using Microsoft.Extensions.Logging;
using Quartz;
using Sidwell.Sync.Application.Contracts.Persistence;

namespace Sidwell.Sync.Jobs;

public abstract class PerTickerJob(IServiceScopeFactory scopeFactory, ILogger logger) : IJob
{
    protected abstract string JobName { get; }

    protected abstract Task SyncOneAsync(IServiceProvider services, string symbol, CancellationToken ct);

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        IReadOnlyList<string> symbols;
        await using (var scope = scopeFactory.CreateAsyncScope())
            symbols = await TrackedSymbols.ResolveAsync(scope.ServiceProvider.GetRequiredService<IUnitOfWork>(), ct);

        logger.LogInformation("{Job}: {Count} tracked tickers", JobName, symbols.Count);

        await Parallel.ForEachAsync(
            symbols,
            new ParallelOptions { MaxDegreeOfParallelism = 5, CancellationToken = ct },
            async (symbol, token) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                try
                {
                    await SyncOneAsync(scope.ServiceProvider, symbol, token);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "{Job}: {Symbol} failed", JobName, symbol);
                }
            });
    }
}
