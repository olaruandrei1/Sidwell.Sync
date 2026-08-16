namespace Sidwell.Sync.Domain.Models;

public readonly record struct DateRange(DateOnly From, DateOnly To)
{
    public static DateRange LastYear(DateOnly to) => new(to.AddYears(-1), to);

    // Initial backfill window: 5+ years of daily bars so fn_price_at_or_before resolves for every
    // fiscal-year-end in `fundamentals` (dcf / ddm / pe_projections / momentum need historical prices).
    public static DateRange LastFiveYears(DateOnly to) => new(to.AddDays(-1825), to);
}
