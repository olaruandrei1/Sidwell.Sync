using System.Text.Json.Serialization;

namespace Sidwell.Sync.Domain.External;

public sealed record SecCompanyTickerEntry(
    [property: JsonPropertyName("cik_str")] long CikStr,
    [property: JsonPropertyName("ticker")] string? Ticker,
    [property: JsonPropertyName("title")] string? Title);
