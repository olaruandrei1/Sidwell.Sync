using System.Globalization;
using Microsoft.Extensions.Options;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.ConfigurableObjects;
using Sidwell.Sync.Domain.Enums;
using Sidwell.Sync.Domain.External;
using Sidwell.Sync.Domain.Helpers;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Infrastructure.ThirdParties.AlphaVantage;

public sealed class AlphaVantageNewsSource(IHttpClientWrapper http, IOptions<AlphaVantageOptions> options) : INewsSource
{
    private readonly AlphaVantageOptions _options = options.Value;

    public DataSource Source => DataSource.AlphaVantage;

    public async Task<IReadOnlyList<NewsArticle>> GetNewsAsync(string symbol, CancellationToken ct = default)
    {
        string normalizedSymbol = SymbolNormalizer.ForExternalApi(symbol);

        if (normalizedSymbol.Contains('.'))
            return [];

        string url = $"query?function=NEWS_SENTIMENT&tickers={Uri.EscapeDataString(normalizedSymbol)}&apikey={_options.ApiKey}";
        AlphaVantageNewsResponse response = await http.GetAsync<AlphaVantageNewsResponse>(url, ct)
            ?? throw new InvalidOperationException($"AlphaVantage returned an empty news response for {symbol}.");

        if (!string.IsNullOrEmpty(response.ErrorMessage))
            throw new InvalidOperationException($"AlphaVantage news error for {symbol}: {response.ErrorMessage}");

        if (!string.IsNullOrEmpty(response.Information))
            throw new InvalidOperationException($"AlphaVantage news information for {symbol}: {response.Information}");

        if (!string.IsNullOrEmpty(response.Note))
            throw new InvalidOperationException($"AlphaVantage news note for {symbol}: {response.Note}");

        if (response.Feed is null)
            return [];

        List<NewsArticle> articles = new List<NewsArticle>(response.Feed.Count);
        
        foreach (var article in response.Feed)
        {
            if (string.IsNullOrWhiteSpace(article.Url) || string.IsNullOrWhiteSpace(article.Title))
                continue;

            articles.Add(new NewsArticle(article.Title, article.Url, ParseTime(article.TimePublished), ResolveSentiment(article, normalizedSymbol)));
        }

        return articles;
    }

    private static DateTimeOffset ParseTime(string? raw) =>
        DateTimeOffset.TryParseExact(raw, "yyyyMMddTHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : DateTimeOffset.UtcNow;

    private static decimal ResolveSentiment(AlphaVantageArticle article, string symbol)
    {
        decimal sentiment = article.OverallSentimentScore;
        AlphaVantageTickerSentiment? match = article.TickerSentiment?.FirstOrDefault(t => string.Equals(t.Ticker, symbol, StringComparison.OrdinalIgnoreCase));
        
        if (match is not null && decimal.TryParse(match.TickerSentimentScore, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            sentiment = parsed;

        return Math.Clamp(sentiment, -1m, 1m);
    }
}
