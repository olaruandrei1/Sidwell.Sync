using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.ConfigurableObjects;
using Sidwell.Sync.Domain.External;
using Sidwell.Sync.Domain.Models;
using Sidwell.Sync.Infrastructure.Implementations.Http;

namespace Sidwell.Sync.Infrastructure.ThirdParties.Sec;

public sealed class SecTickerListSource(
    IHttpClientFactory httpClientFactory,
    IOptions<SecOptions> options,
    ILogger<SecTickerListSource> logger
) : ITickerListSource
{
    public async Task<IReadOnlyList<DiscoveredTicker>> FetchAsync(string? exchange = null, CancellationToken ct = default)
    {
        HttpClientWrapper wrapper = new(httpClientFactory.CreateClient("sec"));

        Dictionary<string, SecCompanyTickerEntry> entries =
            await wrapper.GetAsync<Dictionary<string, SecCompanyTickerEntry>>(options.Value.CompanyTickersUrl, ct) ?? [];

        List<DiscoveredTicker> result = entries.Values
            .Where(e => !string.IsNullOrWhiteSpace(e.Ticker))
            .Select(e => new DiscoveredTicker(
                Symbol: e.Ticker!.ToUpperInvariant(),
                Name: e.Title ?? string.Empty,
                Exchange: "US",
                Currency: "USD",
                Country: "US",
                AssetType: "EQUITY",
                SecCik: e.CikStr.ToString("D10")
            ))
            .GroupBy(t => t.Symbol)
            .Select(g => g.First())
            .ToList();

        logger.LogInformation("SEC ticker list fetched: {Count} tickers", result.Count);

        return result;
    }
}
