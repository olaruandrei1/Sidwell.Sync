using System.Text.Json.Serialization;

namespace Sidwell.Sync.Domain.External;

public sealed record FrankfurterResponse(
    [property: JsonPropertyName("base")] string? Base,
    [property: JsonPropertyName("date")] string? Date,
    [property: JsonPropertyName("rates")] Dictionary<string, decimal>? Rates);
