using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.Entities;
using Sidwell.Sync.Domain.Enums;

namespace Sidwell.Sync.Infrastructure.Implementations.Sources;

public sealed class SourceRouter(IEnumerable<IPriceSource> priceSources) : ISourceRouter
{
    // TwelveData leads: it supports a start/end date range + 5000 points, so the initial 5-year backfill actually
    // gets historical fiscal-year prices. AlphaVantage free tier can only return the last 100 compact bars, so it is
    // the fallback (fine for daily incremental top-ups).
    private static readonly DataSource[] UsChain = [DataSource.TwelveData, DataSource.AlphaVantage];
    private static readonly DataSource[] NonUsChain = [DataSource.Yahoo];

    private static readonly HashSet<string> NonUsMarketSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "RO", "L", "DE", "F", "BE", "MU", "SG", "DU", "HM", "STU", "AS", "PA", "BR", "MI", "MC",
        "LS", "VI", "SW", "ST", "HE", "OL", "CO", "IR", "AT", "WA", "PR", "BD",
    };

    public IReadOnlyList<IPriceSource> ResolvePriceSources(Ticker ticker)
    {
        var chain = IsNonUsMarket(ticker.Symbol) ? NonUsChain : UsChain;

        var resolved = chain
            .Select(source => priceSources.FirstOrDefault(s => s.Source == source))
            .OfType<IPriceSource>()
            .ToList();

        if (resolved.Count == 0)
            throw new InvalidOperationException($"No price source registered for {ticker.Symbol}.");

        return resolved;
    }

    private static bool IsNonUsMarket(string symbol)
    {
        var dot = symbol.LastIndexOf('.');
        return dot > 0 && dot < symbol.Length - 1 && NonUsMarketSuffixes.Contains(symbol[(dot + 1)..]);
    }
}
