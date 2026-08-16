using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.ConfigurableObjects;
using Sidwell.Sync.Domain.External;
using Sidwell.Sync.Infrastructure.Implementations.Http;

namespace Sidwell.Sync.Infrastructure.ThirdParties.Sec;

public sealed class SecCikResolver(
    IHttpClientFactory httpClientFactory,
    IOptions<SecOptions> options,
    ILogger<SecCikResolver> logger
) : ISecCikResolver
{
    private readonly SecOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, string>? _cache;

    public async Task<string?> ResolveAsync(string symbol, CancellationToken ct = default)
    {
        Dictionary<string, string> map = await GetMapAsync(ct);

        return map.GetValueOrDefault(symbol.ToUpperInvariant());
    }

    private async Task<Dictionary<string, string>> GetMapAsync(CancellationToken ct)
    {
        if (_cache is not null)
            return _cache;

        await _gate.WaitAsync(ct);

        try
        {
            if (_cache is not null)
                return _cache;

            HttpClientWrapper wrapper = new(httpClientFactory.CreateClient("sec"));

            Dictionary<string, SecCompanyTickerEntry> entries = 
                await wrapper.GetAsync<Dictionary<string, SecCompanyTickerEntry>>(_options.CompanyTickersUrl, ct) ?? [];

            _cache = entries.Values
                .Where(e => !string.IsNullOrWhiteSpace(e.Ticker))
                .GroupBy(e => e.Ticker!.ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.First().CikStr.ToString("D10"));

            logger.LogInformation("SEC CIK map loaded: {Count} tickers", _cache.Count);

            return _cache;
        }
        finally
        {
            _gate.Release();
        }
    }
}
