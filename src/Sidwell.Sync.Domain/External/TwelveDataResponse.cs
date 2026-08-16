using System.Text.Json.Serialization;

namespace Sidwell.Sync.Domain.External;

public sealed record TwelveDataResponse(
    [property: JsonPropertyName("values")] IReadOnlyList<TwelveDataValue>? Values,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("code")] int Code,
    [property: JsonPropertyName("message")] string? Message);

public sealed record TwelveDataValue(
    [property: JsonPropertyName("datetime")] string Datetime,
    [property: JsonPropertyName("open")] string Open,
    [property: JsonPropertyName("high")] string High,
    [property: JsonPropertyName("low")] string Low,
    [property: JsonPropertyName("close")] string Close,
    [property: JsonPropertyName("volume")] string Volume);
