using System.Text.Json.Serialization;

namespace Sidwell.Sync.Domain.External;

public sealed record AlphaVantageDailyResponse(
    [property: JsonPropertyName("Time Series (Daily)")] Dictionary<string, AlphaVantageDailyBar>? TimeSeries,
    [property: JsonPropertyName("Error Message")] string? ErrorMessage,
    [property: JsonPropertyName("Information")] string? Information,
    [property: JsonPropertyName("Note")] string? Note);

public sealed record AlphaVantageDailyBar(
    [property: JsonPropertyName("1. open")] string Open,
    [property: JsonPropertyName("2. high")] string High,
    [property: JsonPropertyName("3. low")] string Low,
    [property: JsonPropertyName("4. close")] string Close,
    [property: JsonPropertyName("5. volume")] string Volume);
