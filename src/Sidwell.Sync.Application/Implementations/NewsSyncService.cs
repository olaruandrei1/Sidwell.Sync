using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sidwell.Sync.Application.Contracts.Application;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Application.Contracts.Persistence;
using Sidwell.Sync.Domain.Entities;
using Sidwell.Sync.Domain.Enums;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Application.Implementations;

public sealed class NewsSyncService(
    IUnitOfWork uow,
    IEnumerable<INewsSource> newsSources,
    IBroadcastPublisher broadcast,
    TimeProvider clock,
    ILogger<NewsSyncService> logger
) : INewsSyncService
{
    private static readonly DataSource[] Order = [DataSource.Finnhub, DataSource.AlphaVantage, DataSource.Marketaux];

    // A newly-inserted article at or below this sentiment fires CRITICAL_NEWS_ALERT to holders/watchers.
    private const decimal CriticalSentimentThreshold = -0.5m;

    private const string FreshSql =
        "SELECT EXISTS(SELECT 1 FROM news_items WHERE ticker_id = @tickerId AND published_at >= @cutoff)";

    private const string InsertSql = """
        INSERT INTO news_items (ticker_id, title, url, published_at, sentiment, source)
        VALUES (@tickerId, @title, @url, @publishedAt, @sentiment, @source)
        ON CONFLICT (url) DO NOTHING;
        """;

    private const string AffectedUsersSql = """
        SELECT user_id FROM holdings WHERE ticker_id = @tickerId AND shares > 0
        UNION
        SELECT user_id FROM watchlist WHERE ticker_id = @tickerId
        """;

    public async Task<int> SyncTickerNewsAsync(string symbol, CancellationToken ct = default)
    {
        Ticker ticker = await uow.Tickers.AsNoTracking().FirstOrDefaultAsync(t => t.Symbol == symbol, ct)
            ?? throw new InvalidOperationException($"Ticker '{symbol}' not found.");

        DateTimeOffset cutoff = clock.GetUtcNow().AddHours(-24);
        
        bool hasFresh = await uow.Dapper.ExecuteScalarAsync<bool>(FreshSql, new { tickerId = ticker.Id, cutoff }, ct);
        
        if (hasFresh)
        {
            logger.LogInformation("News {Symbol}: fresh news already present, skipped", symbol);

            return 0;
        }

        IReadOnlyList<NewsArticle> articles = await FetchWithFallbackAsync(symbol, ct);
        
        int inserted = 0;
        NewsArticle? worst = null;

        foreach (var article in articles)
        {
            if (string.IsNullOrWhiteSpace(article.Url) || string.IsNullOrWhiteSpace(article.Title))
                continue;

            int rows = await uow.Dapper.ExecuteAsync(InsertSql, new
            {
                tickerId = ticker.Id,
                title = article.Title,
                url = article.Url,
                publishedAt = article.PublishedAt,
                sentiment = article.Sentiment,
                source = article.Source,
            }, ct);

            inserted += rows;

            if (rows > 0 && article.Sentiment is { } s && s <= CriticalSentimentThreshold &&
                (worst is null || s < worst.Sentiment))
                worst = article;
        }

        logger.LogInformation("News {Symbol}: {Inserted} new articles", symbol, inserted);

        if (worst is not null)
            await EmitCriticalNewsAsync(ticker, worst, ct);

        return inserted;
    }

    // Best-effort per-user alert on a strongly negative fresh article. Never breaks the news sync.
    private async Task EmitCriticalNewsAsync(Ticker ticker, NewsArticle article, CancellationToken ct)
    {
        try
        {
            IReadOnlyList<Guid> userIds =
                await uow.Dapper.QueryAsync<Guid>(AffectedUsersSql, new { tickerId = ticker.Id }, ct);

            if (userIds.Count == 0)
                return;

            var payload = new
            {
                symbol = ticker.Symbol,
                tickerId = ticker.Id.ToString(),
                title = article.Title,
                url = article.Url,
                sentiment = article.Sentiment,
                publishedAt = article.PublishedAt,
            };

            foreach (Guid userId in userIds)
                await broadcast.PublishAsync("CRITICAL_NEWS_ALERT", userId, payload, ct);

            logger.LogInformation("CRITICAL_NEWS_ALERT {Symbol} ({Sentiment}) -> {Users} user(s)",
                ticker.Symbol, article.Sentiment, userIds.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Critical-news alert failed for {Symbol} (non-fatal)", ticker.Symbol);
        }
    }

    private async Task<IReadOnlyList<NewsArticle>> FetchWithFallbackAsync(string symbol, CancellationToken ct)
    {
        foreach (var source in Order.Select(d => newsSources.FirstOrDefault(s => s.Source == d)).OfType<INewsSource>())
        {
            try
            {
                IReadOnlyList<NewsArticle> articles = await source.GetNewsAsync(symbol, ct);

                if (articles.Count > 0)
                    return articles.Select(a => a.Source is null ? a with { Source = source.Source.ToString() } : a).ToList();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "News source {Source} failed for {Symbol}, trying next", source.Source, symbol);
            }
        }

        return [];
    }
}
