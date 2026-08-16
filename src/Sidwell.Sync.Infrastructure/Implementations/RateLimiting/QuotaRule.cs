using Sidwell.Sync.Domain.Enums;

namespace Sidwell.Sync.Infrastructure.Implementations.RateLimiting;

public readonly record struct QuotaRule(int MaxPerMinute, int MaxPerDay, TimeSpan MinInterval);

public static class QuotaRules
{
    public static readonly IReadOnlyDictionary<DataSource, QuotaRule> ByApi = new Dictionary<DataSource, QuotaRule>
    {
        [DataSource.Finnhub] = new(MaxPerMinute: 55, MaxPerDay: 0, MinInterval: TimeSpan.FromMilliseconds(1100)),
        [DataSource.AlphaVantage] = new(MaxPerMinute: 0, MaxPerDay: 23, MinInterval: TimeSpan.FromSeconds(12)),
        [DataSource.TwelveData] = new(MaxPerMinute: 25, MaxPerDay: 800, MinInterval: TimeSpan.FromMilliseconds(800)),
        [DataSource.Marketaux] = new(MaxPerMinute: 0, MaxPerDay: 90, MinInterval: TimeSpan.FromSeconds(2)),
        [DataSource.SecEdgar] = new(MaxPerMinute: 10, MaxPerDay: 0, MinInterval: TimeSpan.FromSeconds(6)),
    };
}
