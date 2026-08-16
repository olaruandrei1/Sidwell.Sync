namespace Sidwell.Sync.Application.Contracts.Application;

public interface ITickerDiscoveryService
{
    Task<int> DiscoverUsAsync(CancellationToken ct = default);
    Task<int> DiscoverEuAsync(IReadOnlyList<string> exchanges, CancellationToken ct = default);
    Task<int> DiscoverBvbAsync(CancellationToken ct = default);
}
