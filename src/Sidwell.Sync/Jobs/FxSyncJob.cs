using Microsoft.Extensions.Logging;
using Quartz;
using Sidwell.Sync.Application.Contracts.Application;

namespace Sidwell.Sync.Jobs;

[DisallowConcurrentExecution]
public sealed class FxSyncJob(IServiceScopeFactory scopeFactory, ILogger<FxSyncJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        try
        {
            await scope.ServiceProvider.GetRequiredService<IFxSyncService>().SyncRatesAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FxSyncJob failed");
        }
    }
}
