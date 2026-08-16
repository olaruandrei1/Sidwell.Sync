using Sidwell.Sync.Infrastructure.Implementations.Http;
using Sidwell.Sync.Infrastructure.ThirdParties.Sec;
using Sidwell.Sync.UnitTests.Fakes;

namespace Sidwell.Sync.UnitTests.Sec;

public sealed class SecFundamentalsSourceTests
{
    private static SecFundamentalsSource Build(string json)
    {
        var http = new HttpClient(new StubHttpMessageHandler(json)) { BaseAddress = new Uri("https://data.sec.gov/") };
        return new SecFundamentalsSource(new HttpClientWrapper(http));
    }

    [Fact]
    public async Task Groups_by_period_and_maps_us_gaap_tags()
    {
        const string json = """
            {"facts":{"us-gaap":{
              "Assets":{"units":{"USD":[{"end":"2023-12-31","val":1000,"form":"10-K","fy":2023,"fp":"FY","filed":"2024-02-01"}]}},
              "Revenues":{"units":{"USD":[{"start":"2023-01-01","end":"2023-12-31","val":500,"form":"10-K","fy":2023,"fp":"FY","filed":"2024-02-01"}]}},
              "NetIncomeLoss":{"units":{"USD":[{"start":"2023-01-01","end":"2023-12-31","val":80,"form":"10-K","fy":2023,"fp":"FY","filed":"2024-02-01"}]}},
              "CostOfGoodsAndServicesSold":{"units":{"USD":[{"start":"2023-01-01","end":"2023-12-31","val":300,"form":"10-K","fy":2023,"fp":"FY","filed":"2024-02-01"}]}}
            }}}
            """;

        var snapshots = await Build(json).GetFundamentalsAsync("0000000001");

        var snapshot = Assert.Single(snapshots);
        Assert.Equal(new DateOnly(2023, 12, 31), snapshot.AsOfDate);
        Assert.Equal("FY", snapshot.Period);
        Assert.Equal(1000m, snapshot.TotalAssets);
        Assert.Equal(500m, snapshot.Revenue);
        Assert.Equal(80m, snapshot.NetIncome);
        Assert.Equal(200m, snapshot.GrossProfit);
    }

    [Fact]
    public async Task Annual_balance_sheet_fact_with_instant_frame_is_retained()
    {
        // SEC labels instant facts with a calendar-quarter instant frame (CY2026Q1I) even inside a 10-K.
        const string json = """
            {"facts":{"us-gaap":{
              "Assets":{"units":{"USD":[{"end":"2026-05-02","val":2295619000,"form":"10-K","fy":2026,"fp":"FY","filed":"2026-06-15","frame":"CY2026Q1I"}]}},
              "Revenues":{"units":{"USD":[{"start":"2025-05-04","end":"2026-05-02","val":1335116000,"form":"10-K","fy":2026,"fp":"FY","filed":"2026-06-15","frame":"CY2025"}]}}
            }}}
            """;

        var snapshot = Assert.Single(await Build(json).GetFundamentalsAsync("0001807794"));

        Assert.Equal("FY", snapshot.Period);
        Assert.Equal(2295619000m, snapshot.TotalAssets);
        Assert.Equal(1335116000m, snapshot.Revenue);
    }

    [Fact]
    public async Task Debt_free_filer_derives_long_term_debt_from_non_current_liabilities()
    {
        const string json = """
            {"facts":{"us-gaap":{
              "Liabilities":{"units":{"USD":[{"end":"2026-05-02","val":232007000,"form":"10-K","fy":2026,"fp":"FY","filed":"2026-06-15"}]}},
              "LiabilitiesCurrent":{"units":{"USD":[{"end":"2026-05-02","val":197091000,"form":"10-K","fy":2026,"fp":"FY","filed":"2026-06-15"}]}}
            }}}
            """;

        var snapshot = Assert.Single(await Build(json).GetFundamentalsAsync("0001807794"));

        Assert.Equal(34916000m, snapshot.LongTermDebt);
    }

    [Fact]
    public async Task Quarterly_duration_fact_in_annual_form_is_still_rejected()
    {
        const string json = """
            {"facts":{"us-gaap":{
              "Assets":{"units":{"USD":[{"end":"2026-05-02","val":100,"form":"10-K","fy":2026,"fp":"FY","filed":"2026-06-15"}]}},
              "Revenues":{"units":{"USD":[{"start":"2026-02-01","end":"2026-05-02","val":407012000,"form":"10-K","fy":2026,"fp":"FY","filed":"2026-06-15","frame":"CY2026Q1"}]}}
            }}}
            """;

        var snapshot = Assert.Single(await Build(json).GetFundamentalsAsync("0001807794"));

        Assert.Null(snapshot.Revenue);
    }

    [Fact]
    public async Task Empty_facts_returns_empty()
    {
        var snapshots = await Build("""{"facts":{"us-gaap":{}}}""").GetFundamentalsAsync("0000000001");
        Assert.Empty(snapshots);
    }
}
