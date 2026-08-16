namespace Sidwell.Sync.Application.Contracts.Infrastructure;

// Fire-and-forget publisher to Sidwell.Broadcasting's internal ingest endpoint. Never throws to the caller.
// userId == null => broadcast to all connected clients (used for global SYNC_* market-data events).
public interface IBroadcastPublisher
{
    Task PublishAsync(string eventName, Guid? userId, object payload, CancellationToken ct = default);
}
