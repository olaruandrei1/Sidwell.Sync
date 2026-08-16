namespace Sidwell.Sync.Domain.Models;

public sealed record FxRate(string Currency, DateOnly RateDate, decimal RateToRon);
