using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Application.Contracts.Infrastructure;

public interface IFilingsSource
{
    Task<IReadOnlyList<SecFilingRecord>> GetFilingsAsync(string cik, CancellationToken ct = default);
}
