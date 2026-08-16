using Sidwell.Sync.Domain.Entities;

namespace Sidwell.Sync.Application.Contracts.Infrastructure;

public interface ISourceRouter
{
    IReadOnlyList<IPriceSource> ResolvePriceSources(Ticker ticker);
}
