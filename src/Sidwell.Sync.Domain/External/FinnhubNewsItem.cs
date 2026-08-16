using System.Text.Json.Serialization;

namespace Sidwell.Sync.Domain.External;

public sealed record FinnhubNewsItem(
    [property: JsonPropertyName("headline")] string? Headline,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("datetime")] long Datetime,
    [property: JsonPropertyName("summary")] string? Summary);
