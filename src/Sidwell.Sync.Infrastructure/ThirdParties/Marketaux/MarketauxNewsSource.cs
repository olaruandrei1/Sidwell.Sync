using System.Globalization;
using Microsoft.Extensions.Options;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.ConfigurableObjects;
using Sidwell.Sync.Domain.Enums;
using Sidwell.Sync.Domain.External;
using Sidwell.Sync.Domain.Helpers;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Infrastructure.ThirdParties.Marketaux;

public sealed class MarketauxNewsSource(IHttpClientWrapper http, IOptions<MarketauxOptions> options) : INewsSource
{
    private readonly MarketauxOptions _options = options.Value;

    public DataSource Source => DataSource.Marketaux;

    public async Task<IReadOnlyList<NewsArticle>> GetNewsAsync(string symbol, CancellationToken ct = default)
    {
        string url = $"news/all?symbols={Uri.EscapeDataString(SymbolNormalizer.ForExternalApi(symbol))}&api_token={_options.ApiKey}";
        
        MarketauxResponse response = await http.GetAsync<MarketauxResponse>(url, ct)
            ?? throw new InvalidOperationException($"Marketaux returned an empty response for {symbol}.");

        if (!string.IsNullOrEmpty(response.Error?.Message))
            throw new InvalidOperationException($"Marketaux error for {symbol}: {response.Error.Message}");

        if (response.Data is null)
            return [];

        List<NewsArticle> articles = new(response.Data.Count);

        foreach (var article in response.Data)
        {
            if (string.IsNullOrWhiteSpace(article.Url) || string.IsNullOrWhiteSpace(article.Title))
                continue;

            decimal? sentiment = null;
            
            MarketauxEntity? entity = article.Entities?.FirstOrDefault(e => string.Equals(e.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
            
            if (entity is not null)
                sentiment = Math.Clamp(entity.SentimentScore, -1m, 1m);

            articles.Add(new NewsArticle(article.Title, article.Url, ParseTime(article.PublishedAt), sentiment));
        }

        return articles;
    }

    private static DateTimeOffset ParseTime(string? raw) =>
        DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : DateTimeOffset.UtcNow;
}
