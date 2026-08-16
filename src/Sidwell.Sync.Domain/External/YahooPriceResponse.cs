using System.Text.Json.Serialization;

namespace Sidwell.Sync.Domain.External;

public sealed record YahooPriceResponse(
    [property: JsonPropertyName("symbol")] string? Symbol,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("bars")] IReadOnlyList<YahooBar>? Bars);

public sealed record YahooBar(
    [property: JsonPropertyName("date")] string? Date,
    [property: JsonPropertyName("open")] string? Open,
    [property: JsonPropertyName("high")] string? High,
    [property: JsonPropertyName("low")] string? Low,
    [property: JsonPropertyName("close")] string? Close,
    [property: JsonPropertyName("volume")] long Volume);
