namespace Sidwell.Sync.Application.Contracts.Infrastructure;

public interface ISyncNotifier
{
    Task ProgressAsync(string symbol, string step, double percent, CancellationToken ct = default);

    Task CompletedAsync(string symbol, CancellationToken ct = default);
}
