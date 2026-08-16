using System.Globalization;
using Microsoft.Extensions.Options;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.ConfigurableObjects;
using Sidwell.Sync.Domain.Enums;
using Sidwell.Sync.Domain.External;
using Sidwell.Sync.Domain.Helpers;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Infrastructure.ThirdParties.AlphaVantage;

public sealed class AlphaVantagePriceSource(IHttpClientWrapper http, IOptions<AlphaVantageOptions> options) : IPriceSource
{
    private readonly AlphaVantageOptions _options = options.Value;

    public DataSource Source => DataSource.AlphaVantage;

    public async Task<IReadOnlyList<PriceBar>> GetDailyPricesAsync(string symbol, DateRange range, CancellationToken ct = default)
    {
        // NOTE: outputsize=full is a PREMIUM AlphaVantage feature for TIME_SERIES_DAILY, so on the free tier we can
        // only ever get the last 100 "compact" bars here. Multi-year backfill must come from a history-capable source
        // (TwelveData date-range / Yahoo bridge) — see PriceSyncService source ordering for wide ranges.
        var url = $"query?function=TIME_SERIES_DAILY&symbol={Uri.EscapeDataString(SymbolNormalizer.ForExternalApi(symbol))}&outputsize=compact&apikey={_options.ApiKey}";

        var response = await http.GetAsync<AlphaVantageDailyResponse>(url, ct)
            ?? throw new InvalidOperationException($"AlphaVantage returned an empty response for {symbol}.");

        return Map(response, symbol, range);
    }

    private static IReadOnlyList<PriceBar> Map(AlphaVantageDailyResponse response, string symbol, DateRange range)
    {
        if (!string.IsNullOrEmpty(response.ErrorMessage))
            throw new InvalidOperationException($"AlphaVantage error for {symbol}: {response.ErrorMessage}");
        if (!string.IsNullOrEmpty(response.Information))
            throw new InvalidOperationException($"AlphaVantage information for {symbol}: {response.Information}");
        if (!string.IsNullOrEmpty(response.Note))
            throw new InvalidOperationException($"AlphaVantage note for {symbol}: {response.Note}");

        if (response.TimeSeries is null || response.TimeSeries.Count == 0)
            return [];

        var bars = new List<PriceBar>(response.TimeSeries.Count);
        foreach (var (dateText, bar) in response.TimeSeries)
        {
            if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            if (date < range.From || date > range.To)
                continue;

            bars.Add(new PriceBar(
                date,
                decimal.Parse(bar.Open, CultureInfo.InvariantCulture),
                decimal.Parse(bar.High, CultureInfo.InvariantCulture),
                decimal.Parse(bar.Low, CultureInfo.InvariantCulture),
                decimal.Parse(bar.Close, CultureInfo.InvariantCulture),
                long.Parse(bar.Volume, CultureInfo.InvariantCulture)));
        }

        bars.Sort((a, b) => a.Date.CompareTo(b.Date));
        return bars;
    }
}
