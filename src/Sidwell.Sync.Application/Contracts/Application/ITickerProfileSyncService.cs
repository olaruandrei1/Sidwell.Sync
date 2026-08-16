namespace Sidwell.Sync.Application.Contracts.Application;

public interface ITickerProfileSyncService
{
    Task<bool> SyncProfileAsync(string symbol, CancellationToken ct = default);
}
