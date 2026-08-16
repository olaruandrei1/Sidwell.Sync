using Microsoft.Extensions.Logging;
using Quartz;
using Sidwell.Sync.Application.Contracts.Application;

namespace Sidwell.Sync.Jobs;

[DisallowConcurrentExecution]
public sealed class ProfileSyncJob(IServiceScopeFactory scopeFactory, ILogger<ProfileSyncJob> logger)
    : PerTickerJob(scopeFactory, logger)
{
    protected override string JobName => nameof(ProfileSyncJob);

    protected override Task SyncOneAsync(IServiceProvider services, string symbol, CancellationToken ct) =>
        services.GetRequiredService<ITickerProfileSyncService>().SyncProfileAsync(symbol, ct);
}
