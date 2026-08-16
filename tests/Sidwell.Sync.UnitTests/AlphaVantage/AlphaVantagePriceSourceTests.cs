using Microsoft.Extensions.Options;
using Sidwell.Sync.Domain.ConfigurableObjects;
using Sidwell.Sync.Domain.Models;
using Sidwell.Sync.Infrastructure.ThirdParties.AlphaVantage;
using Sidwell.Sync.Infrastructure.Implementations.Http;
using Sidwell.Sync.UnitTests.Fakes;

namespace Sidwell.Sync.UnitTests.AlphaVantage;

public sealed class AlphaVantagePriceSourceTests
{
    private static readonly DateRange Range = new(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 2));

    private static AlphaVantagePriceSource Build(string json)
    {
        var http = new HttpClient(new StubHttpMessageHandler(json)) { BaseAddress = new Uri("https://www.alphavantage.co/") };
        return new AlphaVantagePriceSource(new HttpClientWrapper(http), Options.Create(new AlphaVantageOptions { ApiKey = "test" }));
    }

    [Fact]
    public async Task Parses_sorts_ascending_and_filters_to_range()
    {
        const string json = """
            {"Time Series (Daily)":{
              "2024-01-03":{"1. open":"9","2. high":"9","3. low":"9","4. close":"9","5. volume":"9"},
              "2024-01-02":{"1. open":"10.6","2. high":"11.2","3. low":"10.4","4. close":"11.0","5. volume":"2000"},
              "2024-01-01":{"1. open":"10.0","2. high":"10.8","3. low":"9.9","4. close":"10.5","5. volume":"1000"}
            }}
            """;

        var bars = await Build(json).GetDailyPricesAsync("AAPL", Range);

        Assert.Equal(2, bars.Count);
        Assert.Equal(new PriceBar(new DateOnly(2024, 1, 1), 10.0m, 10.8m, 9.9m, 10.5m, 1000), bars[0]);
        Assert.Equal(new DateOnly(2024, 1, 2), bars[1].Date);
        Assert.Equal(11.0m, bars[1].Close);
    }

    [Fact]
    public async Task Body_level_note_throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Build("""{"Note":"Thank you for using Alpha Vantage, rate limit reached"}""").GetDailyPricesAsync("AAPL", Range));
    }
}
