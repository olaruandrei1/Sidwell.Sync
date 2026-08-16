namespace Sidwell.Sync.Domain.Models;

public sealed record FundamentalSnapshot
{
    public required DateOnly AsOfDate { get; init; }
    public required string Period { get; init; }

    public decimal? Revenue { get; init; }
    public decimal? NetIncome { get; init; }
    public decimal? GrossProfit { get; init; }
    public decimal? Ebit { get; init; }
    public decimal? TotalAssets { get; init; }
    public decimal? TotalLiabilities { get; init; }
    public decimal? TotalEquity { get; init; }
    public decimal? Cash { get; init; }
    public decimal? RetainedEarnings { get; init; }
    public decimal? CurrentAssets { get; init; }
    public decimal? CurrentLiabilities { get; init; }
    public decimal? LongTermDebt { get; init; }
    public decimal? OperatingCashFlow { get; init; }
    public decimal? Capex { get; init; }
    public decimal? FreeCashFlow { get; init; }
    public decimal? Eps { get; init; }
    public decimal? DividendPerShare { get; init; }
    public long? SharesOutstanding { get; init; }
    public decimal? AccountsReceivable { get; init; }
    public decimal? PpeNet { get; init; }
    public decimal? Depreciation { get; init; }
    public decimal? SgaExpense { get; init; }
}
