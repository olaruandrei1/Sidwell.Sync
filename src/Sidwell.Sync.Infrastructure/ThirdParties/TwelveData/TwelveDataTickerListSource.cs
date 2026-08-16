using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.Models;
using Sidwell.Sync.Infrastructure.Implementations.Http;

namespace Sidwell.Sync.Infrastructure.ThirdParties.TwelveData;

public sealed class TwelveDataTickerListSource(
    IHttpClientFactory httpClientFactory,
    ILogger<TwelveDataTickerListSource> logger
) : ITickerListSource
{
    private static readonly Dictionary<string, string> CountryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Germany"] = "DE",
        ["Netherlands"] = "NL",
        ["United Kingdom"] = "GB",
        ["France"] = "FR",
        ["Switzerland"] = "CH",
        ["Sweden"] = "SE",
        ["Denmark"] = "DK",
        ["Norway"] = "NO",
        ["Finland"] = "FI",
        ["Spain"] = "ES",
        ["Italy"] = "IT",
        ["Belgium"] = "BE",
        ["Austria"] = "AT",
        ["Portugal"] = "PT",
        ["Ireland"] = "IE",
        ["Romania"] = "RO"
    };

    private static readonly Dictionary<string, string> ExchangeSuffixMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["XETRA"] = ".DE",
        ["XETR"] = ".DE",
        ["Euronext Amsterdam"] = ".AS",
        ["XAMS"] = ".AS",
        ["Euronext Paris"] = ".PA",
        ["XPAR"] = ".PA",
        ["Euronext Milan"] = ".MI",
        ["XMIL"] = ".MI",
        ["Euronext Brussels"] = ".BR",
        ["Euronext Lisbon"] = ".LS",
        ["XLIS"] = ".LS",
        ["Madrid"] = ".MC",
        ["XMAD"] = ".MC",
        ["Helsinki"] = ".HE",
        ["Oslo"] = ".OL",
        ["Copenhagen"] = ".CO",
        ["Swiss"] = ".SW",
        ["SIX"] = ".SW",
        ["XSWX"] = ".SW",
        ["Stockholm"] = ".ST",
        ["XSTO"] = ".ST",
        ["Vienna"] = ".VI",
        ["Irish"] = ".IR",
        ["Bucharest"] = ".RO",
        ["BVB"] = ".RO",
        ["XBSE"] = ".RO",
        ["XLON"] = ".L",
        ["LSE"] = ".L"
    };

    public async Task<IReadOnlyList<DiscoveredTicker>> FetchAsync(string? exchange = null, CancellationToken ct = default)
    {
        HttpClientWrapper wrapper = new(httpClientFactory.CreateClient("twelvedata"));

        List<DiscoveredTicker> result = [];

        string url = $"/stocks?exchange={exchange}";

        TwelveDataStocksResponse? response = await wrapper.GetAsync<TwelveDataStocksResponse>(url, ct);

        if (response?.Data is not null)
        {
            foreach (TwelveDataStock stock in response.Data)
            {
                string symbol = stock.Symbol;
                string suffix = ResolveSuffix(stock.Exchange);

                if (!string.IsNullOrEmpty(suffix) && !symbol.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    symbol += suffix;

                string? country = CountryMap.GetValueOrDefault(stock.Country);
                string assetType = string.Equals(stock.Type, "ETF", StringComparison.OrdinalIgnoreCase) ? "ETF" : "EQUITY";

                result.Add(new DiscoveredTicker(
                    Symbol: symbol,
                    Name: stock.Name,
                    Exchange: stock.Exchange,
                    Currency: stock.Currency,
                    Country: country,
                    AssetType: assetType,
                    SecCik: null
                ));
            }
        }

        logger.LogInformation("TwelveData ticker list fetched for {Exchange}: {Count} tickers", exchange, result.Count);

        return result;
    }

    private static string ResolveSuffix(string exchange)
    {
        foreach (KeyValuePair<string, string> pair in ExchangeSuffixMap)
        {
            if (exchange.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        }

        return string.Empty;
    }

    private sealed record TwelveDataStocksResponse(
        [property: JsonPropertyName("data")] List<TwelveDataStock>? Data
    );

    private sealed record TwelveDataStock(
        [property: JsonPropertyName("symbol")] string Symbol,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("currency")] string Currency,
        [property: JsonPropertyName("exchange")] string Exchange,
        [property: JsonPropertyName("country")] string Country,
        [property: JsonPropertyName("type")] string Type
    );
}
