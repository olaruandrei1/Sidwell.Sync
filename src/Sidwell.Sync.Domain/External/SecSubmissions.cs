using System.Text.Json.Serialization;

namespace Sidwell.Sync.Domain.External;

public sealed record SecSubmissions(
    [property: JsonPropertyName("filings")] SecFilings? Filings);

public sealed record SecFilings(
    [property: JsonPropertyName("recent")] SecRecentFilings? Recent);

public sealed record SecRecentFilings(
    [property: JsonPropertyName("accessionNumber")] IReadOnlyList<string>? AccessionNumber,
    [property: JsonPropertyName("form")] IReadOnlyList<string>? Form,
    [property: JsonPropertyName("filingDate")] IReadOnlyList<string>? FilingDate);
