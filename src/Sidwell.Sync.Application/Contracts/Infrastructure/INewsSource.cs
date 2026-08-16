using Sidwell.Sync.Domain.Enums;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Application.Contracts.Infrastructure;

public interface INewsSource
{
    DataSource Source { get; }

    Task<IReadOnlyList<NewsArticle>> GetNewsAsync(string symbol, CancellationToken ct = default);
}
