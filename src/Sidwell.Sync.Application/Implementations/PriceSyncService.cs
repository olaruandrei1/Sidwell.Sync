using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sidwell.Sync.Application.Contracts.Application;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Application.Contracts.Persistence;
using Sidwell.Sync.Domain.Entities;
using Sidwell.Sync.Domain.Enums;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Application.Implementations;

public sealed class PriceSyncService(
    IUnitOfWork uow,
    ISourceRouter router,
    IRecalcTrigger recalcTrigger,
    ISyncNotifier notifier,
    TimeProvider clock,
    ILogger<PriceSyncService> logger) : IPriceSyncService
{
    private const string LatestDateSql =
        "SELECT max(date) FROM price_history WHERE ticker_id = @tickerId";

    private const string UpsertSql = """
        INSERT INTO price_history (ticker_id, date, open, high, low, close, volume, source)
        SELECT @tickerId, d, o, h, l, c, v, @source
        FROM unnest(@dates::date[], @opens::numeric[], @highs::numeric[], @lows::numeric[],
                    @closes::numeric[], @volumes::bigint[]) AS t(d, o, h, l, c, v)
        ON CONFLICT (ticker_id, date) DO UPDATE SET
            open = EXCLUDED.open, high = EXCLUDED.high, low = EXCLUDED.low,
            close = EXCLUDED.close, volume = EXCLUDED.volume, source = EXCLUDED.source;
        """;

    public async Task<PriceSyncResult> SyncTickerAsync(string symbol, bool forceRefresh = false, CancellationToken ct = default)
    {
        Ticker ticker = await uow.Tickers.AsNoTracking().FirstOrDefaultAsync(t => t.Symbol == symbol, ct)
            ?? throw new InvalidOperationException($"Ticker '{symbol}' not found.");

        IReadOnlyList<IPriceSource> sources = router.ResolvePriceSources(ticker);

        SyncJob job = await StartJobAsync(sources[0].Source, ct);

        try
        {
            await notifier.ProgressAsync(symbol, "fetching_prices", 10, ct);

            DateOnly today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            DateOnly? latest = await uow.Dapper.ExecuteScalarAsync<DateOnly?>(LatestDateSql, new { tickerId = ticker.Id }, ct);

            if (!forceRefresh && latest is { } current && current >= today)
            {
                await FinishJobAsync(job, sources[0].Source, ct);

                logger.LogInformation("Sync {Symbol}: up to date (latest {Latest}), skipped", symbol, current);

                return new PriceSyncResult(symbol, ticker.Id, sources[0].Source, 0, Skipped: true);
            }

            // First sync of a ticker (no history yet) => backfill 5 years so historical fiscal-year prices exist.
            // When latest == today, still re-request today (rather than today+1, which would be an empty
            // range) — today's bar may have been captured mid-session and be stale/partial; the upsert
            // below overwrites it once the finalized close is available.
            DateRange range = latest is { } last
                ? new DateRange(last < today ? last.AddDays(1) : today, today)
                : DateRange.LastFiveYears(today);
            
            (IReadOnlyList<PriceBar>? bars, DataSource? usedSource) = await FetchWithFallbackAsync(sources, symbol, range, ct);
            
            DataSource effectiveSource = usedSource ?? sources[0].Source;

            int written = bars.Count > 0
                ? await UpsertBarsAsync(ticker.Id, effectiveSource.ToString().ToLowerInvariant(), bars, ct)
                : 0;

            await FinishJobAsync(job, effectiveSource, ct);

            await recalcTrigger.TriggerAsync(ticker.Id, today, ct);
            await notifier.CompletedAsync(symbol, ct);

            if (written == 0)
            {
                // All configured sources ran without throwing but returned zero usable bars for the
                // requested range (e.g. every close came back NaN/blank) — the ticker's price silently
                // stays on its previous value. Surfaced as a warning (not error) since a real holiday/
                // no-trading day looks identical from here; worth checking source coverage if this
                // repeats for the same symbol across multiple sync attempts.
                logger.LogWarning(
                    "Sync {Symbol} via {Source}: 0 bars written for range {From}..{To} — price will remain unchanged",
                    symbol, effectiveSource, range.From, range.To);
            }
            else
            {
                logger.LogInformation("Sync {Symbol} via {Source}: {Written} bars written", symbol, effectiveSource, written);
            }

            return new PriceSyncResult(symbol, ticker.Id, effectiveSource, written, Skipped: false);
        }
        catch (Exception ex)
        {
            await FailJobAsync(job, ex, ct);

            logger.LogError(ex, "Sync {Symbol} failed", symbol);

            throw;
        }
    }

    private async Task<(IReadOnlyList<PriceBar> Bars, DataSource? Source)> FetchWithFallbackAsync(
        IReadOnlyList<IPriceSource> sources, string symbol, DateRange range, CancellationToken ct)
    {
        foreach (var source in sources)
        {
            try
            {
                var bars = await source.GetDailyPricesAsync(symbol, range, ct);
                if (bars.Count > 0)
                    return (bars, source.Source);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Source {Source} failed for {Symbol}, trying next", source.Source, symbol);
            }
        }

        return ([], null);
    }

    private Task<int> UpsertBarsAsync(Guid tickerId, string source, IReadOnlyList<PriceBar> bars, CancellationToken ct) =>
        uow.Dapper.ExecuteAsync(UpsertSql, new
        {
            tickerId,
            source,
            dates = bars.Select(b => b.Date).ToArray(),
            opens = bars.Select(b => b.Open).ToArray(),
            highs = bars.Select(b => b.High).ToArray(),
            lows = bars.Select(b => b.Low).ToArray(),
            closes = bars.Select(b => b.Close).ToArray(),
            volumes = bars.Select(b => b.Volume).ToArray(),
        }, ct);

    private async Task<SyncJob> StartJobAsync(DataSource source, CancellationToken ct)
    {
        var job = new SyncJob
        {
            Id = Guid.NewGuid(),
            Source = source.ToString().ToLowerInvariant(),
            Status = SyncJobStatus.Running,
            StartedAt = clock.GetUtcNow(),
        };
        uow.SyncJobs.Add(job);
        await uow.SaveChangesAsync(ct);
        return job;
    }

    private async Task FinishJobAsync(SyncJob job, DataSource source, CancellationToken ct)
    {
        job.Source = source.ToString().ToLowerInvariant();
        job.Status = SyncJobStatus.Succeeded;
        job.FinishedAt = clock.GetUtcNow();
        await uow.SaveChangesAsync(ct);
    }

    private async Task FailJobAsync(SyncJob job, Exception ex, CancellationToken ct)
    {
        job.Status = SyncJobStatus.Failed;
        job.FinishedAt = clock.GetUtcNow();
        job.Error = ex.Message;
        try { await uow.SaveChangesAsync(ct); }
        catch { }
    }
}
