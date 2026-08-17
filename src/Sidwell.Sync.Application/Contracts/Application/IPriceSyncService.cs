using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Application.Contracts.Application;

public interface IPriceSyncService
{
    /// <param name="forceRefresh">
    /// When true, re-fetches today's bar even if one is already stored (it may have been captured
    /// mid-session and be stale/partial). The scheduled bulk sync job leaves this false to avoid
    /// re-fetching every ticker's "today" bar on every run; user-triggered single-symbol syncs
    /// (the manual Sync button) pass true.
    /// </param>
    Task<PriceSyncResult> SyncTickerAsync(string symbol, bool forceRefresh = false, CancellationToken ct = default);
}
