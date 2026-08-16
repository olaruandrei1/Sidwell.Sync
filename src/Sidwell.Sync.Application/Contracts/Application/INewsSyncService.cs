namespace Sidwell.Sync.Application.Contracts.Application;

public interface INewsSyncService
{
    Task<int> SyncTickerNewsAsync(string symbol, CancellationToken ct = default);
}
