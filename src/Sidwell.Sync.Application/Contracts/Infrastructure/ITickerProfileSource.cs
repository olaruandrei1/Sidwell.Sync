using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Application.Contracts.Infrastructure;

public interface ITickerProfileSource
{
    Task<TickerProfile?> GetProfileAsync(string symbol, CancellationToken ct = default);
}
