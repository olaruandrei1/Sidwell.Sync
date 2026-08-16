namespace Sidwell.Sync.Application.Contracts.Application;

public interface ITickerAnalysisSyncService
{
    Task<bool> SyncTickerAnalysisAsync(string symbol, CancellationToken ct = default);
}
