namespace Sidwell.Sync.Domain.Models;

public sealed record SecSyncResult(string Symbol, string Cik, int FundamentalsUpserted, int FilingsUpserted);
