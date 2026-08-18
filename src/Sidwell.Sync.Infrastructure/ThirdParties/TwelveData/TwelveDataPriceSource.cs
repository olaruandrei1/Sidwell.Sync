using System.Globalization;
using Microsoft.Extensions.Options;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.ConfigurableObjects;
using Sidwell.Sync.Domain.Enums;
using Sidwell.Sync.Domain.External;
using Sidwell.Sync.Domain.Helpers;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Infrastructure.ThirdParties.TwelveData;

public sealed class TwelveDataPriceSource(IHttpClientWrapper http, IOptions<TwelveDataOptions> options) : IPriceSource
{
    private readonly TwelveDataOptions _options = options.Value;

    public DataSource Source => DataSource.TwelveData;

    public async Task<IReadOnlyList<PriceBar>> GetDailyPricesAsync(string symbol, DateRange range, CancellationToken ct = default)
    {
        // TwelveData's API rejects a single-day range (start_date == end_date) with HTTP 400 — widen
        // the request by a day in that case (e.g. force-refreshing "today") and filter back down to
        // what was actually asked for.
        DateOnly requestFrom = range.From == range.To ? range.From.AddDays(-1) : range.From;

        string url = $"time_series?symbol={Uri.EscapeDataString(SymbolNormalizer.ForExternalApi(symbol))}&interval=1day" +
                  $"&start_date={requestFrom:yyyy-MM-dd}&end_date={range.To:yyyy-MM-dd}" +
                  $"&outputsize=5000&apikey={_options.ApiKey}";

        TwelveDataResponse response = await http.GetAsync<TwelveDataResponse>(url, ct)
            ?? throw new InvalidOperationException($"TwelveData returned an empty response for {symbol}.");

        return Map(response, symbol).Where(b => b.Date >= range.From).ToList();
    }

    private static IReadOnlyList<PriceBar> Map(TwelveDataResponse response, string symbol)
    {
        if (response.Status == "error")
            throw new InvalidOperationException($"TwelveData error ({response.Code}) for {symbol}: {response.Message}");

        if (response.Values is null || response.Values.Count == 0)
            return [];

        List<PriceBar> bars = new List<PriceBar>(response.Values.Count);
        
        foreach (var value in response.Values)
        {
            if (!DateOnly.TryParseExact(value.Datetime, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;

            bars.Add(new PriceBar(
                date,
                decimal.Parse(value.Open, CultureInfo.InvariantCulture),
                decimal.Parse(value.High, CultureInfo.InvariantCulture),
                decimal.Parse(value.Low, CultureInfo.InvariantCulture),
                decimal.Parse(value.Close, CultureInfo.InvariantCulture),
                long.Parse(value.Volume, CultureInfo.InvariantCulture)));
        }

        bars.Sort((a, b) => a.Date.CompareTo(b.Date));

        return bars;
    }
}
