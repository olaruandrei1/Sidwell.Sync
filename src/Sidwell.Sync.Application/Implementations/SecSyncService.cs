using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sidwell.Sync.Application.Contracts.Application;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Application.Contracts.Persistence;
using Sidwell.Sync.Domain.Entities;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Application.Implementations;

public sealed class SecSyncService(
    IUnitOfWork uow,
    ISecCikResolver cikResolver,
    IFundamentalsSource fundamentalsSource,
    IFilingsSource filingsSource,
    IRecalcTrigger recalcTrigger,
    ILogger<SecSyncService> logger
) : ISecSyncService
{
    private const string FundamentalUpsertSql = """
        INSERT INTO fundamentals (ticker_id, as_of_date, period, revenue, net_income, gross_profit, ebit,
            total_assets, total_liabilities, total_equity, cash, market_cap, retained_earnings, current_assets, current_liabilities,
            long_term_debt, operating_cash_flow, capex, free_cash_flow, eps, dividend_per_share, dividend_yield, pe_ratio, shares_outstanding,
            accounts_receivable, ppe_net, depreciation, sga_expense)
        VALUES (@tickerId, @asOfDate, @period, @revenue, @netIncome, @grossProfit, @ebit,
            @totalAssets, @totalLiabilities, @totalEquity, @cash, @marketCap, @retainedEarnings, @currentAssets, @currentLiabilities,
            @longTermDebt, @operatingCashFlow, @capex, @freeCashFlow, @eps, @dividendPerShare, @dividendYield, @peRatio, @sharesOutstanding,
            @accountsReceivable, @ppeNet, @depreciation, @sgaExpense)
        ON CONFLICT (ticker_id, as_of_date, period) DO UPDATE SET
            revenue = EXCLUDED.revenue, net_income = EXCLUDED.net_income, gross_profit = EXCLUDED.gross_profit,
            ebit = EXCLUDED.ebit, total_assets = EXCLUDED.total_assets, total_liabilities = EXCLUDED.total_liabilities,
            total_equity = EXCLUDED.total_equity, cash = EXCLUDED.cash, market_cap = COALESCE(EXCLUDED.market_cap, fundamentals.market_cap),
            retained_earnings = EXCLUDED.retained_earnings,
            current_assets = EXCLUDED.current_assets, current_liabilities = EXCLUDED.current_liabilities,
            long_term_debt = EXCLUDED.long_term_debt, operating_cash_flow = EXCLUDED.operating_cash_flow,
            capex = EXCLUDED.capex, free_cash_flow = EXCLUDED.free_cash_flow, eps = EXCLUDED.eps,
            dividend_per_share = EXCLUDED.dividend_per_share,
            dividend_yield = COALESCE(EXCLUDED.dividend_yield, fundamentals.dividend_yield),
            pe_ratio = COALESCE(EXCLUDED.pe_ratio, fundamentals.pe_ratio),
            shares_outstanding = EXCLUDED.shares_outstanding, accounts_receivable = EXCLUDED.accounts_receivable,
            ppe_net = EXCLUDED.ppe_net, depreciation = EXCLUDED.depreciation, sga_expense = EXCLUDED.sga_expense;
        """;

    // SEC filings never carry market cap; derive it from shares_outstanding * the close on/before the fiscal date.
    private const string PriceOnOrBeforeSql =
        "SELECT close FROM price_history WHERE ticker_id = @tickerId AND date <= @date ORDER BY date DESC LIMIT 1;";

    private const string FilingUpsertSql = """
        INSERT INTO sec_filings (ticker_id, form_type, filing_date, accession_no)
        VALUES (@tickerId, @formType, @filingDate, @accessionNo)
        ON CONFLICT (accession_no) DO NOTHING;
        """;

    public async Task<SecSyncResult> SyncAsync(string symbol, CancellationToken ct = default)
    {
        Ticker ticker = await uow.Tickers.FirstOrDefaultAsync(t => t.Symbol == symbol, ct)
            ?? throw new InvalidOperationException($"Ticker '{symbol}' not found.");

        string? cik = await ResolveCikAsync(ticker.Id, symbol, ticker.SecCik, ct);
        
        if (cik is null)
        {
            logger.LogWarning("SEC {Symbol}: no CIK found (non-US ticker?)", symbol);
        
            return new SecSyncResult(symbol, string.Empty, 0, 0);
        }

        IReadOnlyList<FundamentalSnapshot> fundamentals = await fundamentalsSource.GetFundamentalsAsync(cik, ct);
        
        int fundamentalsUpserted = 0;

        foreach (var snapshot in fundamentals)
        {
            // One close lookup drives market_cap, pe_ratio and dividend_yield (all price-relative, SEC never reports them).
            decimal? close = await uow.Dapper.ExecuteScalarAsync<decimal?>(
                PriceOnOrBeforeSql, new { tickerId = ticker.Id, date = snapshot.AsOfDate }, ct);

            decimal? marketCap = close is { } p1 && snapshot.SharesOutstanding is { } shares && shares > 0 ? p1 * shares : null;
            decimal? peRatio = close is { } p2 && snapshot.Eps is { } eps && eps > 0 ? p2 / eps : null;
            decimal? dividendYield = close is { } p3 && p3 > 0 && snapshot.DividendPerShare is { } dps && dps > 0 ? dps / p3 : null;

            fundamentalsUpserted += await uow.Dapper.ExecuteAsync(
                FundamentalUpsertSql, ToParameters(ticker.Id, snapshot, marketCap, peRatio, dividendYield), ct);
        }

        IReadOnlyList<SecFilingRecord> filings = await filingsSource.GetFilingsAsync(cik, ct);
        int filingsUpserted = 0;

        foreach (var filing in filings)
            filingsUpserted += await uow.Dapper.ExecuteAsync(FilingUpsertSql, new
            {
                tickerId = ticker.Id,
                formType = filing.FormType,
                filingDate = filing.FilingDate,
                accessionNo = filing.AccessionNo,
            }, ct);

        var latestFundamentalDate = fundamentals
            .Where(f => f.Period == "FY")
            .Select(f => (DateOnly?)f.AsOfDate)
            .Max();
        if (latestFundamentalDate is { } asOf)
            await recalcTrigger.TriggerAsync(ticker.Id, asOf, ct);

        logger.LogInformation("SEC {Symbol} (CIK {Cik}): {Fundamentals} fundamentals, {Filings} filings", symbol, cik, fundamentalsUpserted, filingsUpserted);
    
        return new SecSyncResult(symbol, cik, fundamentalsUpserted, filingsUpserted);
    }

    private async Task<string?> ResolveCikAsync(Guid tickerId, string symbol, string? existingCik, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(existingCik))
            return existingCik.PadLeft(10, '0');

        string? resolved = await cikResolver.ResolveAsync(symbol, ct);
        
        if (resolved is null)
            return null;

        await uow.Tickers.Where(t => t.Id == tickerId).ExecuteUpdateAsync(s => s.SetProperty(t => t.SecCik, resolved), ct);
        
        return resolved;
    }

    private static object ToParameters(Guid tickerId, FundamentalSnapshot snapshot, decimal? marketCap, decimal? peRatio, decimal? dividendYield) => new
    {
        tickerId,
        asOfDate = snapshot.AsOfDate,
        period = snapshot.Period,
        revenue = snapshot.Revenue,
        netIncome = snapshot.NetIncome,
        grossProfit = snapshot.GrossProfit,
        ebit = snapshot.Ebit,
        totalAssets = snapshot.TotalAssets,
        totalLiabilities = snapshot.TotalLiabilities,
        totalEquity = snapshot.TotalEquity,
        cash = snapshot.Cash,
        marketCap,
        retainedEarnings = snapshot.RetainedEarnings,
        currentAssets = snapshot.CurrentAssets,
        currentLiabilities = snapshot.CurrentLiabilities,
        longTermDebt = snapshot.LongTermDebt,
        operatingCashFlow = snapshot.OperatingCashFlow,
        capex = snapshot.Capex,
        freeCashFlow = snapshot.FreeCashFlow,
        eps = snapshot.Eps,
        dividendPerShare = snapshot.DividendPerShare,
        dividendYield,
        peRatio,
        sharesOutstanding = snapshot.SharesOutstanding,
        accountsReceivable = snapshot.AccountsReceivable,
        ppeNet = snapshot.PpeNet,
        depreciation = snapshot.Depreciation,
        sgaExpense = snapshot.SgaExpense,
    };
}
