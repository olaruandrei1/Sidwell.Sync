using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Application.Contracts.Application;

public interface IPriceSyncService
{
    Task<PriceSyncResult> SyncTickerAsync(string symbol, CancellationToken ct = default);
}
