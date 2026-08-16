using Sidwell.Sync.Domain.Models;
using Sidwell.Sync.Infrastructure.Implementations.Http;
using Sidwell.Sync.Infrastructure.ThirdParties.Yahoo;
using Sidwell.Sync.UnitTests.Fakes;

namespace Sidwell.Sync.UnitTests.Yahoo;

public sealed class YahooBridgePriceSourceTests
{
    private static readonly DateRange Range = new(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 5));

    private static YahooBridgePriceSource Build(string json)
    {
        var http = new HttpClient(new StubHttpMessageHandler(json)) { BaseAddress = new Uri("http://localhost:8000/") };
        return new YahooBridgePriceSource(new HttpClientWrapper(http));
    }

    [Fact]
    public async Task Maps_string_prices_to_decimal_and_sorts()
    {
        const string json = """
            {"symbol":"TLV.RO","currency":"RON","bars":[
              {"date":"2024-01-03","open":"10.6","high":"11.2","low":"10.4","close":"11.0","volume":2000},
              {"date":"2024-01-02","open":"10.0","high":"10.8","low":"9.9","close":"10.5","volume":1000}
            ]}
            """;

        var bars = await Build(json).GetDailyPricesAsync("TLV.RO", Range);

        Assert.Equal(2, bars.Count);
        Assert.Equal(new PriceBar(new DateOnly(2024, 1, 2), 10.0m, 10.8m, 9.9m, 10.5m, 1000), bars[0]);
        Assert.Equal(11.0m, bars[1].Close);
        Assert.Equal(2000, bars[1].Volume);
    }

    [Fact]
    public async Task Empty_bars_returns_empty()
    {
        var bars = await Build("""{"symbol":"X.RO","currency":"RON","bars":[]}""").GetDailyPricesAsync("X.RO", Range);
        Assert.Empty(bars);
    }
}
