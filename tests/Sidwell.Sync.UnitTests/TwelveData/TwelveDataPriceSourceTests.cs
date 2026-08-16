using Microsoft.Extensions.Options;
using Sidwell.Sync.Domain.ConfigurableObjects;
using Sidwell.Sync.Domain.Models;
using Sidwell.Sync.Infrastructure.Implementations.Http;
using Sidwell.Sync.Infrastructure.ThirdParties.TwelveData;
using Sidwell.Sync.UnitTests.Fakes;

namespace Sidwell.Sync.UnitTests.TwelveData;

public sealed class TwelveDataPriceSourceTests
{
    private static readonly DateRange Range = new(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 2));

    private static TwelveDataPriceSource Build(string json)
    {
        var http = new HttpClient(new StubHttpMessageHandler(json)) { BaseAddress = new Uri("https://api.twelvedata.com/") };
        return new TwelveDataPriceSource(new HttpClientWrapper(http), Options.Create(new TwelveDataOptions { ApiKey = "test" }));
    }

    [Fact]
    public async Task Parses_and_sorts_ascending()
    {
        const string json = """
            {"status":"ok","values":[
              {"datetime":"2024-01-02","open":"10.6","high":"11.2","low":"10.4","close":"11.0","volume":"2000"},
              {"datetime":"2024-01-01","open":"10.0","high":"10.8","low":"9.9","close":"10.5","volume":"1000"}
            ]}
            """;

        var bars = await Build(json).GetDailyPricesAsync("AAPL", Range);

        Assert.Equal(2, bars.Count);
        Assert.Equal(new PriceBar(new DateOnly(2024, 1, 1), 10.0m, 10.8m, 9.9m, 10.5m, 1000), bars[0]);
        Assert.Equal(11.0m, bars[1].Close);
    }

    [Fact]
    public async Task Error_status_throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Build("""{"status":"error","code":429,"message":"limit reached"}""").GetDailyPricesAsync("AAPL", Range));
    }
}
