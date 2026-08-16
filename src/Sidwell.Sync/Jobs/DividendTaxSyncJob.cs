using Microsoft.Extensions.Logging;
using Quartz;
using Sidwell.Sync.Application.Contracts.Application;

namespace Sidwell.Sync.Jobs;

[DisallowConcurrentExecution]
public sealed class DividendTaxSyncJob(IServiceScopeFactory scopeFactory, ILogger<DividendTaxSyncJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        try
        {
            await scope.ServiceProvider.GetRequiredService<IDividendTaxSyncService>().SyncDividendTaxRatesAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DividendTaxSyncJob failed");
        }
    }
}
