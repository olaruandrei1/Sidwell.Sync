namespace Sidwell.Sync.Application.Contracts.Application;

public interface IDividendTaxSyncService
{
    Task<int> SyncDividendTaxRatesAsync(CancellationToken ct = default);
}
