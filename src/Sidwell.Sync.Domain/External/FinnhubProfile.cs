using System.Text.Json.Serialization;

namespace Sidwell.Sync.Domain.External;

public sealed record FinnhubProfile(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("exchange")] string? Exchange,
    [property: JsonPropertyName("country")] string? Country,
    [property: JsonPropertyName("finnhubIndustry")] string? Industry,
    [property: JsonPropertyName("type")] string? Type);
