using Microsoft.Extensions.Logging;
using Quartz;
using Sidwell.Sync.Application.Contracts.Application;

namespace Sidwell.Sync.Jobs;

[DisallowConcurrentExecution]
public sealed class PriceSyncJob(IServiceScopeFactory scopeFactory, ILogger<PriceSyncJob> logger)
    : PerTickerJob(scopeFactory, logger)
{
    protected override string JobName => nameof(PriceSyncJob);

    protected override Task SyncOneAsync(IServiceProvider services, string symbol, CancellationToken ct) =>
        services.GetRequiredService<IPriceSyncService>().SyncTickerAsync(symbol, ct);
}
