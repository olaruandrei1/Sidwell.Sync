using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.Entities;
using Sidwell.Sync.Domain.Enums;
using Sidwell.Sync.Domain.Models;
using Sidwell.Sync.Infrastructure.Implementations.Sources;

namespace Sidwell.Sync.UnitTests.Sources;

public sealed class SourceRouterTests
{
    private sealed class StubSource(DataSource source) : IPriceSource
    {
        public DataSource Source => source;
        public Task<IReadOnlyList<PriceBar>> GetDailyPricesAsync(string symbol, DateRange range, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PriceBar>>([]);
    }

    private static SourceRouter Router() => new(
    [
        new StubSource(DataSource.Yahoo),
        new StubSource(DataSource.TwelveData),
        new StubSource(DataSource.AlphaVantage),
    ]);

    [Fact]
    public void Us_ticker_routes_to_alphavantage_then_twelvedata()
    {
        var chain = Router().ResolvePriceSources(new Ticker { Symbol = "AAPL", Exchange = "NASDAQ" });
        Assert.Equal([DataSource.AlphaVantage, DataSource.TwelveData], chain.Select(s => s.Source));
    }

    [Theory]
    [InlineData("TLV.RO")]
    [InlineData("VOW.DE")]
    [InlineData("SHEL.L")]
    [InlineData("ASML.AS")]
    public void Non_us_ticker_routes_only_to_yahoo(string symbol)
    {
        var chain = Router().ResolvePriceSources(new Ticker { Symbol = symbol });
        Assert.Equal([DataSource.Yahoo], chain.Select(s => s.Source));
    }

    [Fact]
    public void Us_class_share_with_single_letter_suffix_stays_us()
    {
        var chain = Router().ResolvePriceSources(new Ticker { Symbol = "BRK.B" });
        Assert.Equal([DataSource.AlphaVantage, DataSource.TwelveData], chain.Select(s => s.Source));
    }

    [Fact]
    public void Throws_when_no_source_registered()
    {
        Assert.Throws<InvalidOperationException>(() => new SourceRouter([]).ResolvePriceSources(new Ticker { Symbol = "AAPL" }));
    }
}
