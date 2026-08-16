namespace Sidwell.Sync.Application.Contracts.Application;

public interface IFxSyncService
{
    Task<int> SyncRatesAsync(CancellationToken ct = default);
    Task<int> SyncRatesAsync(IReadOnlyList<string> currencies, CancellationToken ct = default);
}
