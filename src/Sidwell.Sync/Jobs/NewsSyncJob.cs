using Microsoft.Extensions.Logging;
using Quartz;
using Sidwell.Sync.Application.Contracts.Application;

namespace Sidwell.Sync.Jobs;

[DisallowConcurrentExecution]
public sealed class NewsSyncJob(IServiceScopeFactory scopeFactory, ILogger<NewsSyncJob> logger)
    : PerTickerJob(scopeFactory, logger)
{
    protected override string JobName => nameof(NewsSyncJob);

    protected override Task SyncOneAsync(IServiceProvider services, string symbol, CancellationToken ct) =>
        services.GetRequiredService<INewsSyncService>().SyncTickerNewsAsync(symbol, ct);
}
