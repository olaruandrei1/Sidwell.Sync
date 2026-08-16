using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.External;
using Sidwell.Sync.Domain.Models;

namespace Sidwell.Sync.Infrastructure.ThirdParties.Sec;

public sealed class SecFilingsSource(IHttpClientWrapper http) : IFilingsSource
{
    private static readonly HashSet<string> TrackedForms = new(StringComparer.OrdinalIgnoreCase) { "10-K", "10-Q", "8-K" };

    public async Task<IReadOnlyList<SecFilingRecord>> GetFilingsAsync(string cik, CancellationToken ct = default)
    {
        SecSubmissions? submissions = await http.GetAsync<SecSubmissions>($"submissions/CIK{cik}.json", ct);
        SecRecentFilings? recent = submissions?.Filings?.Recent;

        if (recent?.AccessionNumber is null || recent.Form is null || recent.FilingDate is null)
            return [];

        int count = Math.Min(recent.AccessionNumber.Count, Math.Min(recent.Form.Count, recent.FilingDate.Count));

        List<SecFilingRecord> filings = new(count);
        
        for (var i = 0; i < count; i++)
        {
            string form = recent.Form[i];

            if (!TrackedForms.Contains(form))
                continue;

            if (!DateOnly.TryParse(recent.FilingDate[i], out var filingDate))
                continue;

            filings.Add(new SecFilingRecord(form, filingDate, recent.AccessionNumber[i]));
        }

        return filings;
    }
}
