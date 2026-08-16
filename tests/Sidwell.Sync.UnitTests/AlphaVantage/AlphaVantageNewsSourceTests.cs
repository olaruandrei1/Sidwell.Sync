using Microsoft.Extensions.Options;
using Sidwell.Sync.Domain.ConfigurableObjects;
using Sidwell.Sync.Infrastructure.ThirdParties.AlphaVantage;
using Sidwell.Sync.Infrastructure.Implementations.Http;
using Sidwell.Sync.UnitTests.Fakes;

namespace Sidwell.Sync.UnitTests.AlphaVantage;

public sealed class AlphaVantageNewsSourceTests
{
    private static AlphaVantageNewsSource Build(string json)
    {
        var http = new HttpClient(new StubHttpMessageHandler(json)) { BaseAddress = new Uri("https://www.alphavantage.co/") };
        return new AlphaVantageNewsSource(new HttpClientWrapper(http), Options.Create(new AlphaVantageOptions { ApiKey = "test" }));
    }

    [Fact]
    public async Task Prefers_ticker_specific_sentiment_and_parses_time()
    {
        const string json = """
            {"feed":[{"title":"Apple beats","url":"https://x/1","time_published":"20240102T120000",
              "overall_sentiment_score":0.3,
              "ticker_sentiment":[{"ticker":"AAPL","ticker_sentiment_score":"0.55"}]}]}
            """;

        var articles = await Build(json).GetNewsAsync("AAPL");

        var article = Assert.Single(articles);
        Assert.Equal("https://x/1", article.Url);
        Assert.Equal(0.55m, article.Sentiment);
        Assert.Equal(new DateTimeOffset(2024, 1, 2, 12, 0, 0, TimeSpan.Zero), article.PublishedAt);
    }

    [Fact]
    public async Task Body_level_note_throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Build("""{"Note":"rate limit reached"}""").GetNewsAsync("AAPL"));
    }
}
