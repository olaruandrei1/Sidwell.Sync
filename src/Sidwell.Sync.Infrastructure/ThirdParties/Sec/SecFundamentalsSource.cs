using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.External;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Infrastructure.ThirdParties.Sec;

public sealed class SecFundamentalsSource(
    IHttpClientWrapper http,
    ILogger<SecFundamentalsSource>? logger = null
) : IFundamentalsSource
{
    private readonly ILogger<SecFundamentalsSource> _logger = logger ?? NullLogger<SecFundamentalsSource>.Instance;

    private static readonly IReadOnlyList<KeyValuePair<string, string[]>> MetricTags =
    [
        new("Revenues", [
            "RevenueFromContractWithCustomerExcludingAssessedTax",
            "RevenueFromContractWithCustomerIncludingAssessedTax",
            "Revenues",
            "SalesRevenueNet",
            "SalesRevenueGoodsNet",
            "SalesRevenueServicesNet",
            "NetRevenues",
            "TotalRevenues",
            "OtherRevenue",
        ]),
        new("NetIncomeLoss", ["NetIncomeLoss", "NetIncomeLossAvailableToCommonStockholdersBasic"]),
        new("EarningsPerShareBasic", ["EarningsPerShareBasic", "EarningsPerShareBasicAndDiluted", "EarningsPerShareDiluted"]),
        new("DividendPerShare", ["CommonStockDividendsPerShareDeclared", "CommonStockDividendsPerShareCashPaid"]),
        new("Assets", ["Assets"]),
        new("Liabilities", ["Liabilities"]),
        new("StockholdersEquity", ["StockholdersEquity", "StockholdersEquityIncludingPortionAttributableToNoncontrollingInterest"]),
        new("Cash", ["CashAndCashEquivalentsAtCarryingValue", "CashAndCashEquivalents", "Cash"]),
        new("OperatingIncomeLoss", ["OperatingIncomeLoss", "IncomeLossFromContinuingOperationsBeforeIncomeTaxesExtraordinaryItemsNoncontrollingInterest"]),
        new("CurrentAssets", ["AssetsCurrent"]),
        new("CurrentLiabilities", ["LiabilitiesCurrent"]),
        new("RetainedEarnings", ["RetainedEarningsAccumulatedDeficit"]),
        new("LongTermDebt", [
            "LongTermDebtNoncurrent",
            "LongTermDebt",
            "ConvertibleDebtNoncurrent",
            "NotesPayableNoncurrent",
            "LongTermLineOfCredit",
            "FinanceLeaseLiabilityNoncurrent",
            "OperatingLeaseLiabilityNoncurrent",
            "LiabilitiesNoncurrent",
        ]),
        new("GrossProfit", ["GrossProfit"]),
        new("CostOfRevenue", ["CostOfGoodsAndServicesSold", "CostOfRevenue", "CostOfGoodsSold"]),
        new("OperatingCashFlow", ["NetCashProvidedByUsedInOperatingActivities", "NetCashProvidedByUsedInOperatingActivitiesContinuingOperations"]),
        new("CapitalExpenditures", ["PaymentsToAcquirePropertyPlantAndEquipment", "PaymentsToAcquireProductiveAssets", "PaymentsForCapitalImprovements"]),
        new("SharesOutstanding", ["CommonStockSharesOutstanding"]),
        new("AccountsReceivable", ["AccountsReceivableNetCurrent"]),
        new("PpeNet", ["PropertyPlantAndEquipmentNet"]),
        new("Depreciation", ["DepreciationDepletionAndAmortization", "DepreciationAmortizationAndAccretionNet", "Depreciation"]),
        new("SgaExpense", ["SellingGeneralAndAdministrativeExpense"]),
        new("SgaMarketing", ["SellingAndMarketingExpense"]),
        new("SgaGA", ["GeneralAndAdministrativeExpense"]),
    ];

    private static readonly HashSet<string> InstantMetrics =
    [
        "Assets", "Liabilities", "StockholdersEquity", "CurrentAssets", "CurrentLiabilities",
        "RetainedEarnings", "LongTermDebt", "SharesOutstanding", "AccountsReceivable", "PpeNet", "Cash",
    ];

    private static readonly HashSet<string> Periods = ["FY", "Q1", "Q2", "Q3", "Q4"];

    public async Task<IReadOnlyList<FundamentalSnapshot>> GetFundamentalsAsync(string cik, CancellationToken ct = default)
    {
        SecCompanyFacts? facts = await http.GetAsync<SecCompanyFacts>($"api/xbrl/companyfacts/CIK{cik}.json", ct);
        
        Dictionary<string, SecFact>? usGaap = facts?.Facts?.UsGaap;
        
        if (usGaap is null || usGaap.Count == 0)
        {
            _logger.LogWarning("SEC fundamentals CIK {Cik}: companyfacts returned no us-gaap taxonomy", cik);

            return [];
        }

        Dictionary<string, Dictionary<string, BestEntry>> grouped = new Dictionary<string, Dictionary<string, BestEntry>>();

        foreach (var (metricName, tags) in MetricTags)
        {
            bool isInstant = InstantMetrics.Contains(metricName);

            for (var tagIndex = 0; tagIndex < tags.Length; tagIndex++)
            {
                if (!usGaap.TryGetValue(tags[tagIndex], out var fact) || fact.Units is null)
                    continue;

                foreach (var entries in fact.Units.Values)
                {
                    foreach (var entry in entries)
                    {
                        string? form = entry.Form;

                        if (form is not ("10-K" or "10-Q" or "20-F" or "6-K") || string.IsNullOrEmpty(entry.End))
                            continue;

                        // Instant (balance-sheet) facts always carry a calendar-quarter instant frame (CY2026Q1I)
                        // whatever form they came from, so annual/quarterly discrimination applies to durations only.
                        bool isInstantFrame = entry.Frame is not null && entry.Frame.EndsWith('I');

                        if (!isInstant && !isInstantFrame && !string.IsNullOrEmpty(entry.Frame))
                        {
                            bool isQuarterlyFrame = entry.Frame.Length >= 8 && entry.Frame[6] == 'Q';

                            if (form is ("10-Q" or "6-K") && !isQuarterlyFrame)
                                continue;

                            if (form is ("10-K" or "20-F") && isQuarterlyFrame)
                                continue;
                        }

                        int score = isInstant ? 100 : DurationScore(form, entry.Start, entry.End);

                        string normalizedForm = form switch { "20-F" => "10-K", "6-K" => "10-Q", _ => form };
                        string key = $"{entry.End}_{normalizedForm}";

                        if (!grouped.TryGetValue(key, out var metrics))
                            grouped[key] = metrics = [];

                        if (ShouldReplace(metrics.GetValueOrDefault(metricName), tagIndex, score, entry.Filed))
                            metrics[metricName] = new BestEntry(entry.Val, score, entry.Filed, entry.Fy ?? 0, entry.Fp, tagIndex);
                    }
                }
            }
        }

        List<FundamentalSnapshot> snapshots = new(grouped.Count);

        foreach (var (key, metrics) in grouped)
        {
            string endText = key[..key.IndexOf('_')];
            
            if (!DateOnly.TryParse(endText, out var endDate))
                continue;

            string? period = metrics.Values.Select(m => m.Fp).FirstOrDefault(fp => !string.IsNullOrEmpty(fp))?.ToUpperInvariant();

            if (period is null || !Periods.Contains(period))
                continue;

            snapshots.Add(BuildSnapshot(endDate, period, metrics));
        }

        // Ghost rows contain only instant (balance-sheet) facts with no income-statement data —
        // they waste slots and confuse YoY algorithms. Remove them before bucketing.
        List<FundamentalSnapshot> validSnapshots = snapshots
            .Where(s => s.Revenue.HasValue || s.NetIncome.HasValue || s.Ebit.HasValue || s.OperatingCashFlow.HasValue)
            .OrderByDescending(s => s.AsOfDate)
            .ToList();

        // Guarantee at least 5 FY rows so YoY algorithms have prior-year data.
        // Then fill remaining slots with recent quarters (up to 8).
        List<FundamentalSnapshot> fyRows = validSnapshots.Where(s => s.Period == "FY").Take(5).ToList();
        List<FundamentalSnapshot> quarterlyRows = validSnapshots.Where(s => s.Period != "FY").Take(8).ToList();

        List<FundamentalSnapshot> result = fyRows
            .Concat(quarterlyRows)
            .OrderByDescending(s => s.AsOfDate)
            .ToList();

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            foreach (var snapshot in result)
                _logger.LogDebug(
                    "SEC fundamentals CIK {Cik}: {AsOf} {Period} revenue={Revenue} assets={Assets} equity={Equity} ltd={LongTermDebt} shares={Shares}",
                    cik, snapshot.AsOfDate, snapshot.Period, snapshot.Revenue, snapshot.TotalAssets,
                    snapshot.TotalEquity, snapshot.LongTermDebt, snapshot.SharesOutstanding);
        }

        _logger.LogInformation(
            "SEC fundamentals CIK {Cik}: {Total} periods parsed, {Valid} valid (non-ghost), {Returned} returned ({AnnualCount} FY, {QCount} Q)",
            cik, snapshots.Count, validSnapshots.Count, result.Count,
            result.Count(s => s.Period == "FY"), result.Count(s => s.Period != "FY"));

        return result;
    }

    private static FundamentalSnapshot BuildSnapshot(DateOnly asOfDate, string period, Dictionary<string, BestEntry> metrics)
    {
        decimal? Value(string name) => metrics.TryGetValue(name, out var entry) ? entry.Val : (decimal?)null;

        decimal? revenue = Value("Revenues");
        decimal? costOfRevenue = Value("CostOfRevenue");
        decimal? grossProfit = Value("GrossProfit") ?? (revenue is { } r && costOfRevenue is { } c ? r - c : (decimal?)null);

        decimal? totalLiabilities = Value("Liabilities");
        decimal? currentLiabilities = Value("CurrentLiabilities");

        // Debt-free filers report no long-term debt tag at all; fall back to total non-current liabilities
        // so leverage-based algorithms see a real zero-ish value instead of NULL.
        decimal? longTermDebt = Value("LongTermDebt")
            ?? (totalLiabilities is { } tl && currentLiabilities is { } cl ? tl - cl : (decimal?)null);

        decimal? operatingCashFlow = Value("OperatingCashFlow");
        decimal? capex = Value("CapitalExpenditures");
        decimal? freeCashFlow = operatingCashFlow is { } ocf && capex is { } cx ? ocf - cx : (decimal?)null;

        // Some filers (e.g. MSFT) report SG&A as two separate line items rather than combined.
        // Sum them when the combined tag is absent.
        decimal? sgaExpense = Value("SgaExpense")
            ?? (Value("SgaMarketing") is { } mkt && Value("SgaGA") is { } ga ? mkt + ga
                : Value("SgaMarketing") ?? Value("SgaGA"));

        return new FundamentalSnapshot
        {
            AsOfDate = asOfDate,
            Period = period,
            Revenue = revenue,
            NetIncome = Value("NetIncomeLoss"),
            GrossProfit = grossProfit,
            Ebit = Value("OperatingIncomeLoss"),
            TotalAssets = Value("Assets"),
            TotalLiabilities = totalLiabilities,
            TotalEquity = Value("StockholdersEquity"),
            Cash = Value("Cash"),
            RetainedEarnings = Value("RetainedEarnings"),
            CurrentAssets = Value("CurrentAssets"),
            CurrentLiabilities = currentLiabilities,
            LongTermDebt = longTermDebt,
            OperatingCashFlow = operatingCashFlow,
            Capex = capex,
            FreeCashFlow = freeCashFlow,
            Eps = Value("EarningsPerShareBasic"),
            DividendPerShare = Value("DividendPerShare"),
            SharesOutstanding = Value("SharesOutstanding") is { } shares ? (long)shares : (long?)null,
            AccountsReceivable = Value("AccountsReceivable"),
            PpeNet = Value("PpeNet"),
            Depreciation = Value("Depreciation"),
            SgaExpense = sgaExpense,
        };
    }

    private static bool ShouldReplace(BestEntry? current, int tagIndex, int score, string? filed)
    {
        if (current is null)
            return true;

        if (tagIndex != current.TagPriority)
            return tagIndex < current.TagPriority;

        return score > current.Score || (score == current.Score && string.CompareOrdinal(filed, current.Filed) > 0);
    }

    private static int DurationScore(string form, string? start, string? end)
    {
        if (!DateOnly.TryParse(start, out var startDate) || !DateOnly.TryParse(end, out var endDate))
            return 1;

        int days = endDate.DayNumber - startDate.DayNumber;

        return form switch
        {
            "10-Q" or "6-K" => days is >= 75 and <= 105 ? 100 : days is >= 60 and <= 120 ? 80 : 10,
            "10-K" or "20-F" => days is >= 330 and <= 390 ? 100 : 10,
            _ => 50,
        };
    }

    private sealed record BestEntry(decimal Val, int Score, string? Filed, int Fy, string? Fp, int TagPriority);
}
