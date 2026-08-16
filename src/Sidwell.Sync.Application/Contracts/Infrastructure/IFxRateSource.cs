using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Application.Contracts.Infrastructure;

public interface IFxRateSource
{
    Task<IReadOnlyList<FxRate>> GetRatesToRonAsync(IReadOnlyList<string> currencies, CancellationToken ct = default);
}
