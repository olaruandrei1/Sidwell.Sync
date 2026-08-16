using Microsoft.Extensions.Logging;
using Quartz;
using Sidwell.Sync.Application.Contracts.Application;

namespace Sidwell.Sync.Jobs;

[DisallowConcurrentExecution]
public sealed class TickerAnalysisSyncJob(IServiceScopeFactory scopeFactory, ILogger<TickerAnalysisSyncJob> logger)
    : PerTickerJob(scopeFactory, logger)
{
    protected override string JobName => nameof(TickerAnalysisSyncJob);

    protected override Task SyncOneAsync(IServiceProvider services, string symbol, CancellationToken ct) =>
        services.GetRequiredService<ITickerAnalysisSyncService>().SyncTickerAnalysisAsync(symbol, ct);
}
