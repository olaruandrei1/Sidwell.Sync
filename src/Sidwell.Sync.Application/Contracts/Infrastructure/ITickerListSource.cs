using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Application.Contracts.Infrastructure;

public interface ITickerListSource
{
    Task<IReadOnlyList<DiscoveredTicker>> FetchAsync(string? exchange = null, CancellationToken ct = default);
}
