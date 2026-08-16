namespace Sidwell.Sync.Domain.Models;

public sealed record SecFilingRecord(string FormType, DateOnly FilingDate, string AccessionNo);
