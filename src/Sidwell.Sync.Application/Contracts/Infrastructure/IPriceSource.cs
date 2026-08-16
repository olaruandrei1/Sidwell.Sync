using Sidwell.Sync.Domain.Enums;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Application.Contracts.Infrastructure;

public interface IPriceSource
{
    DataSource Source { get; }

    Task<IReadOnlyList<PriceBar>> GetDailyPricesAsync(string symbol, DateRange range, CancellationToken ct = default);
}
