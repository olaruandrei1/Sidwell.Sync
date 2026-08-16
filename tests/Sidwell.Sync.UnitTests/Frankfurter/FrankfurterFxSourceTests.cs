using Sidwell.Sync.Infrastructure.ThirdParties.Frankfurter;
using Sidwell.Sync.Infrastructure.Implementations.Http;
using Sidwell.Sync.UnitTests.Fakes;

namespace Sidwell.Sync.UnitTests.Frankfurter;

public sealed class FrankfurterFxSourceTests
{
    private static FrankfurterFxSource Build(string json)
    {
        var http = new HttpClient(new StubHttpMessageHandler(json)) { BaseAddress = new Uri("https://api.frankfurter.app/") };
        return new FrankfurterFxSource(new HttpClientWrapper(http), TimeProvider.System);
    }

    [Fact]
    public async Task Inverts_ron_base_rate_into_rate_to_ron()
    {
        const string json = """{"base":"RON","date":"2024-01-02","rates":{"USD":0.2,"EUR":0.25}}""";

        var rates = await Build(json).GetRatesToRonAsync(["USD", "EUR", "RON"]);

        Assert.Equal(2, rates.Count);
        var usd = rates.Single(r => r.Currency == "USD");
        Assert.Equal(5.0m, usd.RateToRon);
        Assert.Equal(new DateOnly(2024, 1, 2), usd.RateDate);
        Assert.Equal(4.0m, rates.Single(r => r.Currency == "EUR").RateToRon);
    }
}
