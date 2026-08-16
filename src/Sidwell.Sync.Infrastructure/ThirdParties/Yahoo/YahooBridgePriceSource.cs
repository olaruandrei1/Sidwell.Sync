using System.Globalization;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.Enums;
using Sidwell.Sync.Domain.External;
using Sidwell.Sync.Domain.Helpers;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Infrastructure.ThirdParties.Yahoo;

public sealed class YahooBridgePriceSource(IHttpClientWrapper http) : IPriceSource
{
    public DataSource Source => DataSource.Yahoo;

    public async Task<IReadOnlyList<PriceBar>> GetDailyPricesAsync(string symbol, DateRange range, CancellationToken ct = default)
    {
        var url = $"api/v1/prices?symbol={Uri.EscapeDataString(SymbolNormalizer.ForExternalApi(symbol))}" +
                  $"&start={range.From:yyyy-MM-dd}&end={range.To:yyyy-MM-dd}";

        var response = await http.GetAsync<YahooPriceResponse>(url, ct)
            ?? throw new InvalidOperationException($"Yahoo bridge returned an empty response for {symbol}.");

        if (response.Bars is null || response.Bars.Count == 0)
            return [];

        var bars = new List<PriceBar>(response.Bars.Count);
        foreach (var bar in response.Bars)
        {
            if (!DateOnly.TryParseExact(bar.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;

            // Yahoo returns "NaN" for in-progress days (session not yet closed). Skip those instead of crashing the batch.
            if (!TryParse(bar.Close, out decimal close) || close <= 0)
                continue;

            _ = TryParse(bar.Open, out decimal open);
            _ = TryParse(bar.High, out decimal high);
            _ = TryParse(bar.Low, out decimal low);

            bars.Add(new PriceBar(date, open, high, low, close, bar.Volume));
        }

        bars.Sort((a, b) => a.Date.CompareTo(b.Date));
        return bars;
    }

    private static bool TryParse(string? value, out decimal result)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "NaN", StringComparison.OrdinalIgnoreCase))
        {
            result = 0m;
            return false;
        }
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
    }
}
