using Microsoft.Extensions.Logging;
using Sidwell.Sync.Application.Contracts.Application;
using Sidwell.Sync.Application.Contracts.Persistence;
using Sidwell.Sync.Domain.Models;
using Sidwell.Sync.Infrastructure.ThirdParties.Finnhub;
using Sidwell.Sync.Infrastructure.ThirdParties.Sec;
using Sidwell.Sync.Infrastructure.ThirdParties.TwelveData;

namespace Sidwell.Sync.Infrastructure.Implementations.Discovery;

public sealed class TickerDiscoveryService(
    IUnitOfWork uow,
    SecTickerListSource secSource,
    TwelveDataTickerListSource twelveDataSource,
    FinnhubTickerListSource finnhubSource,
    ILogger<TickerDiscoveryService> logger
) : ITickerDiscoveryService
{
    private const string UpsertSql = """
        INSERT INTO tickers (id, symbol, name, exchange, currency, country, asset_type, sec_cik, discovery_source, discovered_at)
        VALUES (gen_random_uuid(), @Symbol, @Name, @Exchange, @Currency, @Country, @AssetType, @SecCik, @Source, now())
        ON CONFLICT (symbol, exchange) DO UPDATE SET
            name = EXCLUDED.name,
            currency = EXCLUDED.currency,
            country = COALESCE(EXCLUDED.country, tickers.country),
            asset_type = COALESCE(EXCLUDED.asset_type, tickers.asset_type),
            sec_cik = COALESCE(EXCLUDED.sec_cik, tickers.sec_cik),
            discovery_source = EXCLUDED.discovery_source,
            discovered_at = EXCLUDED.discovered_at;
        """;

    public async Task<int> DiscoverUsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<DiscoveredTicker> tickers = await secSource.FetchAsync(ct: ct);

        int count = await UpsertAllAsync(tickers, "SEC", ct);

        logger.LogInformation("US discovery complete: {Count} tickers upserted", count);

        return count;
    }

    public async Task<int> DiscoverEuAsync(IReadOnlyList<string> exchanges, CancellationToken ct = default)
    {
        int total = 0;

        foreach (string exchange in exchanges)
        {
            IReadOnlyList<DiscoveredTicker> tickers = await twelveDataSource.FetchAsync(exchange, ct);

            total += await UpsertAllAsync(tickers, "TWELVE_DATA", ct);
        }

        logger.LogInformation("EU discovery complete: {Count} tickers upserted across {Exchanges} exchanges", total, exchanges.Count);

        return total;
    }

    public async Task<int> DiscoverBvbAsync(CancellationToken ct = default)
    {
        IReadOnlyList<DiscoveredTicker> tickers = [];
        string source = "TWELVE_DATA";

        try
        {
            tickers = await twelveDataSource.FetchAsync("BVB", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TwelveData BVB discovery failed, falling back to curated BVB index tickers");
        }

        if (tickers.Count == 0)
        {
            source = "BVB_CURATED";
            tickers = GetCuratedBvbTickers();
        }

        int count = await UpsertAllAsync(tickers, source, ct);

        logger.LogInformation("BVB discovery complete: {Count} tickers upserted from {Source}", count, source);

        return count;
    }

    private static IReadOnlyList<DiscoveredTicker> GetCuratedBvbTickers()
    {
        string[] symbols =
        [
            "TLV.RO", "SNP.RO", "SNG.RO", "BRD.RO", "H2O.RO", "DIGI.RO",
            "FP.RO", "EL.RO", "TGN.RO", "M.RO", "ONE.RO", "SFG.RO",
            "WINE.RO", "AQ.RO", "BVB.RO", "TRP.RO", "ALR.RO", "AAG.RO",
            "COTE.RO", "EVER.RO", "LION.RO", "TRAN.RO", "TTS.RO", "SNN.RO"
        ];

        return symbols.Select(s => new DiscoveredTicker(
            Symbol: s,
            Name: s.Replace(".RO", " S.A."),
            Exchange: "BVB",
            Currency: "RON",
            Country: "RO",
            AssetType: "EQUITY",
            SecCik: null
        )).ToList();
    }

    private async Task<int> UpsertAllAsync(IReadOnlyList<DiscoveredTicker> tickers, string source, CancellationToken ct)
    {
        int count = 0;

        foreach (DiscoveredTicker t in tickers)
        {
            await uow.Dapper.ExecuteAsync(UpsertSql, new
            {
                t.Symbol,
                t.Name,
                t.Exchange,
                t.Currency,
                t.Country,
                t.AssetType,
                t.SecCik,
                Source = source
            }, ct);

            count++;
        }

        return count;
    }
}
