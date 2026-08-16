using Microsoft.Extensions.Logging;
using Sidwell.Sync.Application.Contracts.Application;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Application.Contracts.Persistence;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Application.Implementations;

public sealed class FxSyncService(
    IUnitOfWork uow,
    IFxRateSource fxRateSource,
    ILogger<FxSyncService> logger
) : IFxSyncService
{
    private const string DistinctCurrenciesSql = """
        SELECT currency FROM tickers WHERE currency IS NOT NULL AND btrim(currency) <> 'RON'
        UNION
        SELECT monthly_income_currency FROM finance_settings WHERE monthly_income_currency IS NOT NULL AND btrim(monthly_income_currency) <> 'RON'
        UNION
        SELECT currency FROM expenses WHERE currency IS NOT NULL AND btrim(currency) <> 'RON'
        UNION
        SELECT currency FROM wealth_allocations WHERE currency IS NOT NULL AND btrim(currency) <> 'RON'
        """;

    private const string UpsertSql = """
        INSERT INTO exchange_rates (currency, rate_date, rate_to_ron, source)
        VALUES (@currency, @rateDate, @rateToRon, 'frankfurter')
        ON CONFLICT (currency, rate_date) DO UPDATE SET
            rate_to_ron = EXCLUDED.rate_to_ron, source = EXCLUDED.source;
        """;

    public async Task<int> SyncRatesAsync(CancellationToken ct = default)
    {
        List<string> currencies = (await uow.Dapper.QueryAsync<string>(DistinctCurrenciesSql, null, ct))
            .Select(c => c.Trim())
            .Where(c => c.Length == 3)
            .ToList();

        if (currencies.Count == 0)
            return 0;

        return await UpsertRatesAsync(currencies, ct);
    }

    public async Task<int> SyncRatesAsync(IReadOnlyList<string> currencies, CancellationToken ct = default)
    {
        List<string> filtered = currencies
            .Select(c => c.Trim().ToUpperInvariant())
            .Where(c => c.Length == 3 && !string.Equals(c, "RON", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (filtered.Count == 0)
            return 0;

        return await UpsertRatesAsync(filtered, ct);
    }

    private async Task<int> UpsertRatesAsync(List<string> currencies, CancellationToken ct)
    {
        IReadOnlyList<FxRate> rates = await fxRateSource.GetRatesToRonAsync(currencies, ct);

        int upserted = 0;

        foreach (var rate in rates)
        {
            upserted += await uow.Dapper.ExecuteAsync(UpsertSql, new
            {
                currency = rate.Currency,
                rateDate = rate.RateDate,
                rateToRon = rate.RateToRon,
            }, ct);
        }

        logger.LogInformation("FX: {Count} rates upserted", upserted);

        return upserted;
    }
}
