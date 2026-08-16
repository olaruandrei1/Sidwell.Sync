using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.ConfigurableObjects;
using Sidwell.Sync.Domain.Models;
using Sidwell.Sync.Infrastructure.Implementations.Http;

namespace Sidwell.Sync.Infrastructure.ThirdParties.Finnhub;

public sealed class FinnhubTickerListSource(
    IHttpClientFactory httpClientFactory,
    IOptions<FinnhubOptions> options,
    ILogger<FinnhubTickerListSource> logger
) : ITickerListSource
{
    public async Task<IReadOnlyList<DiscoveredTicker>> FetchAsync(string? exchange = null, CancellationToken ct = default)
    {
        HttpClientWrapper wrapper = new(httpClientFactory.CreateClient("finnhub"));

        string url = $"/stock/symbol?exchange={exchange}&token={options.Value.ApiKey}";

        List<FinnhubSymbolEntry>? entries = await wrapper.GetAsync<List<FinnhubSymbolEntry>>(url, ct);

        if (entries is null)
            return [];

        List<DiscoveredTicker> result = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Symbol))
            .Select(e =>
            {
                string symbol = e.Symbol;

                if (!symbol.EndsWith(".RO", StringComparison.OrdinalIgnoreCase))
                    symbol += ".RO";

                return new DiscoveredTicker(
                    Symbol: symbol,
                    Name: e.Description ?? string.Empty,
                    Exchange: "BVB",
                    Currency: e.Currency ?? "RON",
                    Country: "RO",
                    AssetType: "EQUITY",
                    SecCik: null
                );
            })
            .ToList();

        logger.LogInformation("Finnhub ticker list fetched for {Exchange}: {Count} tickers", exchange, result.Count);

        return result;
    }

    private sealed record FinnhubSymbolEntry(
        [property: JsonPropertyName("symbol")] string Symbol,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("currency")] string? Currency,
        [property: JsonPropertyName("type")] string? Type
    );
}
