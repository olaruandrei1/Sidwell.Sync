using Sidwell.Sync.Application.Contracts.Persistence;

namespace Sidwell.Sync.Jobs;

internal static class TrackedSymbols
{
    private const string Sql = """
        SELECT DISTINCT t.symbol
        FROM tickers t
        WHERE EXISTS (SELECT 1 FROM watchlist w WHERE w.ticker_id = t.id)
           OR EXISTS (SELECT 1 FROM holdings h WHERE h.ticker_id = t.id)
        """;

    public static Task<IReadOnlyList<string>> ResolveAsync(IUnitOfWork uow, CancellationToken ct) =>
        uow.Dapper.QueryAsync<string>(Sql, null, ct);
}
