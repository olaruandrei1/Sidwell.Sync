using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sidwell.Sync.Application.Contracts.Application;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Application.Contracts.Persistence;
using Sidwell.Sync.Domain.Entities;
using Sidwell.Sync.Domain.External;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Application.Implementations;

public sealed class TickerAnalysisSyncService(
    IUnitOfWork uow,
    IGeminiClient gemini,
    IRedisService redis,
    TimeProvider clock,
    ILogger<TickerAnalysisSyncService> logger
) : ITickerAnalysisSyncService
{
    private static readonly TimeSpan AnalysisTtl = TimeSpan.FromHours(48);

    private static readonly JsonSerializerOptions StoreOptions = new(JsonSerializerDefaults.Web);

    private const string NewsTitlesSql =
        "SELECT title FROM news_items WHERE ticker_id = @tickerId ORDER BY published_at DESC LIMIT 15";

    private const string OhlcvSql = """
        SELECT date, open, high, low, close, volume
        FROM price_history
        WHERE ticker_id = @tickerId
        ORDER BY date DESC
        LIMIT 30
        """;

    public async Task<bool> SyncTickerAnalysisAsync(string symbol, CancellationToken ct = default)
    {
        Ticker ticker = await uow.Tickers.AsNoTracking().FirstOrDefaultAsync(t => t.Symbol == symbol, ct)
            ?? throw new InvalidOperationException($"Ticker '{symbol}' not found.");

        IReadOnlyList<string> newsTitles = await uow.Dapper.QueryAsync<string>(NewsTitlesSql, new { tickerId = ticker.Id }, ct);
        IReadOnlyList<PriceBar> ohlcv = await uow.Dapper.QueryAsync<PriceBar>(OhlcvSql, new { tickerId = ticker.Id }, ct);

        string? newsSummary = await gemini.SummarizeNewsAsync(newsTitles, ct);
        SentimentResult? sentiment = await gemini.AnalyzeSentimentAsync(symbol, newsTitles, ct);
        Synthesis? synthesis = await gemini.SynthesizeTickerAsync(symbol, ohlcv, newsSummary, ct);

        if (newsSummary is null && sentiment is null && synthesis is null)
        {
            logger.LogWarning("Analysis {Symbol}: all Gemini calls returned null, nothing stored", symbol);

            return false;
        }

        var snapshot = new TickerAnalysisSnapshot(symbol, newsSummary, sentiment, synthesis, clock.GetUtcNow());

        await redis.SetAsync(AnalysisKey(symbol), JsonSerializer.Serialize(snapshot, StoreOptions), AnalysisTtl, ct);

        logger.LogInformation(
            "Analysis {Symbol}: stored (summary={HasSummary}, sentiment={HasSentiment}, synthesis={HasSynthesis})",
            symbol, newsSummary is not null, sentiment is not null, synthesis is not null);

        return true;
    }

    private static string AnalysisKey(string symbol) => $"sidwell:analysis:{symbol.ToUpperInvariant()}";
}
