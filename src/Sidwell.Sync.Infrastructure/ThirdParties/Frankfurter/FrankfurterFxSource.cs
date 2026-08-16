using System.Globalization;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.External;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Infrastructure.ThirdParties.Frankfurter;

public sealed class FrankfurterFxSource(IHttpClientWrapper http, TimeProvider clock) : IFxRateSource
{
    public async Task<IReadOnlyList<FxRate>> GetRatesToRonAsync(IReadOnlyList<string> currencies, CancellationToken ct = default)
    {
        List<string> targets = currencies
            .Where(c => !string.Equals(c, "RON", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targets.Count == 0)
            return [];

        string url = $"latest?from=RON&to={string.Join(',', targets)}";

        FrankfurterResponse response = await http.GetAsync<FrankfurterResponse>(url, ct)
            ?? throw new InvalidOperationException("Frankfurter returned an empty response.");

        if (response.Rates is null || response.Rates.Count == 0)
            return [];

        DateOnly rateDate = DateOnly.TryParseExact(response.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        List<FxRate> rates = new List<FxRate>(response.Rates.Count);
        
        foreach (var (currency, ronToCurrency) in response.Rates)
        {
            if (ronToCurrency == 0)
                continue;

            rates.Add(new FxRate(currency, rateDate, decimal.Round(1m / ronToCurrency, 6)));
        }

        return rates;
    }
}
