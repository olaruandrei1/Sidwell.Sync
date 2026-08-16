using Microsoft.Extensions.Options;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.ConfigurableObjects;
using Sidwell.Sync.Domain.External;
using Sidwell.Sync.Domain.Helpers;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Infrastructure.ThirdParties.Finnhub;

public sealed class FinnhubProfileSource(IHttpClientWrapper http, IOptions<FinnhubOptions> options) : ITickerProfileSource
{
    private readonly FinnhubOptions _options = options.Value;

    public async Task<TickerProfile?> GetProfileAsync(string symbol, CancellationToken ct = default)
    {
        var url = $"stock/profile2?symbol={Uri.EscapeDataString(SymbolNormalizer.ForExternalApi(symbol))}&token={_options.ApiKey}";
        var profile = await http.GetAsync<FinnhubProfile>(url, ct);

        if (profile is null || string.IsNullOrWhiteSpace(profile.Name))
            return null;

        return new TickerProfile(profile.Name, profile.Currency, NormalizeExchange(profile.Exchange));
    }

    // Finnhub returns verbose exchange names ("NASDAQ NMS - GLOBAL MARKET", "NEW YORK STOCK EXCHANGE, INC.")
    // that overflow the varchar(20) tickers.exchange column. Map the common ones to short codes; cap the rest.
    private static string? NormalizeExchange(string? exchange)
    {
        if (string.IsNullOrWhiteSpace(exchange))
            return exchange;

        string upper = exchange.Trim().ToUpperInvariant();

        if (upper.Contains("NASDAQ")) return "NASDAQ";
        if (upper.Contains("NEW YORK") || upper.Contains("NYSE")) return "NYSE";
        if (upper.Contains("AMEX") || upper.Contains("AMERICAN STOCK")) return "AMEX";
        if (upper.Contains("ARCA")) return "NYSE ARCA";
        if (upper.Contains("BATS") || upper.Contains("CBOE")) return "CBOE";

        return upper.Length <= 20 ? upper : upper[..20];
    }
}
