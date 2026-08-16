using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Application.Contracts.Infrastructure;

public interface IFundamentalsSource
{
    Task<IReadOnlyList<FundamentalSnapshot>> GetFundamentalsAsync(string cik, CancellationToken ct = default);
}
