using System.Text.Json.Serialization;

namespace Sidwell.Sync.Domain.External;

public sealed record GeminiDividendTaxResponse(
    [property: JsonPropertyName("rates")] IReadOnlyList<GeminiDividendTaxRate>? Rates);

public sealed record GeminiDividendTaxRate(
    [property: JsonPropertyName("country_code")] string? CountryCode,
    [property: JsonPropertyName("rate_percent")] decimal RatePercent,
    [property: JsonPropertyName("notes")] string? Notes,
    [property: JsonPropertyName("source_url")] string? SourceUrl);
