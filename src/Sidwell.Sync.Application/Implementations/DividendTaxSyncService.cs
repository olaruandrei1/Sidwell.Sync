using Microsoft.Extensions.Logging;
using Sidwell.Sync.Application.Contracts.Application;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Application.Contracts.Persistence;
using Sidwell.Sync.Domain.External;

namespace Sidwell.Sync.Application.Implementations;

public sealed class DividendTaxSyncService(
    IUnitOfWork uow,
    IGeminiClient gemini,
    ILogger<DividendTaxSyncService> logger
) : IDividendTaxSyncService
{
    private static readonly string[] Countries = ["DE", "AT", "CH", "UK", "NL", "RO", "US", "ES", "SE"];

    private const decimal MinRate = 0m;
    private const decimal MaxRate = 60m;

    private const string UpsertSql = """
        INSERT INTO dividend_tax_rates (country_code, rate_percent, notes, source_url, fetched_at)
        VALUES (@countryCode, @ratePercent, @notes, @sourceUrl, now())
        ON CONFLICT (country_code) DO UPDATE SET
            rate_percent = EXCLUDED.rate_percent,
            notes = EXCLUDED.notes,
            source_url = EXCLUDED.source_url,
            fetched_at = now();
        """;

    public async Task<int> SyncDividendTaxRatesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<GeminiDividendTaxRate>? rates = await gemini.FetchDividendTaxRatesAsync(Countries, ct);

        if (rates is null || rates.Count == 0)
        {
            logger.LogWarning("DividendTax: Gemini returned no rates, nothing upserted");

            return 0;
        }

        HashSet<string> requested = new(Countries, StringComparer.OrdinalIgnoreCase);

        int upserted = 0;

        foreach (var rate in rates)
        {
            string? code = rate.CountryCode?.Trim().ToUpperInvariant();

            if (string.IsNullOrEmpty(code) || !requested.Contains(code))
            {
                logger.LogWarning("DividendTax: skipping unexpected country '{Code}'", rate.CountryCode);

                continue;
            }

            if (rate.RatePercent < MinRate || rate.RatePercent > MaxRate)
            {
                logger.LogWarning("DividendTax: rejecting implausible rate {Rate} for {Code}, keeping existing value", rate.RatePercent, code);

                continue;
            }

            upserted += await uow.Dapper.ExecuteAsync(UpsertSql, new
            {
                countryCode = code,
                ratePercent = rate.RatePercent,
                notes = rate.Notes,
                sourceUrl = rate.SourceUrl,
            }, ct);
        }

        logger.LogInformation("DividendTax: {Upserted}/{Total} country rates upserted", upserted, Countries.Length);

        return upserted;
    }
}
