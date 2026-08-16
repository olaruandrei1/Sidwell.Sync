using Microsoft.Extensions.Logging;
using Sidwell.Sync.Application.Contracts.Infrastructure;

namespace Sidwell.Sync.Infrastructure.Implementations.Notifications;

// Broadcasts fine-grained progress for a running sync step (currently the price step) as SYNC_PROGRESS,
// in addition to logging. The authoritative SYNC_STARTED / SYNC_COMPLETE per step are emitted at the
// endpoint boundary (see Program.cs). All SYNC_* events are global (userId null => all clients).
public sealed class BroadcastSyncNotifier(
    IBroadcastPublisher publisher,
    ILogger<BroadcastSyncNotifier> logger
) : ISyncNotifier
{
    public async Task ProgressAsync(string symbol, string step, double percent, CancellationToken ct = default)
    {
        logger.LogInformation("[sync] {Symbol}: {Step} ({Percent:0}%)", symbol, step, percent);

        await publisher.PublishAsync("SYNC_PROGRESS", null, new { symbol, step, percent }, ct);
    }

    public async Task CompletedAsync(string symbol, CancellationToken ct = default)
    {
        logger.LogInformation("[sync] {Symbol}: completed", symbol);

        await publisher.PublishAsync("SYNC_PROGRESS", null, new { symbol, step = "prices", percent = 100d }, ct);
    }
}
