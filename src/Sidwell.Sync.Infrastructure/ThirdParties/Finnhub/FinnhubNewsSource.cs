using Microsoft.Extensions.Options;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.ConfigurableObjects;
using Sidwell.Sync.Domain.Enums;
using Sidwell.Sync.Domain.External;
using Sidwell.Sync.Domain.Helpers;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Infrastructure.ThirdParties.Finnhub;

public sealed class FinnhubNewsSource(IHttpClientWrapper http, IOptions<FinnhubOptions> options) : INewsSource
{
    private readonly FinnhubOptions _options = options.Value;

    public DataSource Source => DataSource.Finnhub;

    public async Task<IReadOnlyList<NewsArticle>> GetNewsAsync(string symbol, CancellationToken ct = default)
    {
        DateOnly to = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly from = to.AddDays(-30);
        
        string url = $"company-news?symbol={Uri.EscapeDataString(SymbolNormalizer.ForExternalApi(symbol))}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&token={_options.ApiKey}";

        IReadOnlyList<FinnhubNewsItem> items = await http.GetAsync<IReadOnlyList<FinnhubNewsItem>>(url, ct) ?? [];

        return items
            .Where(i => !string.IsNullOrWhiteSpace(i.Url) && !string.IsNullOrWhiteSpace(i.Headline))
            .Select(i => new NewsArticle(i.Headline!, i.Url!, DateTimeOffset.FromUnixTimeSeconds(i.Datetime), null))
            .ToList();
    }
}
