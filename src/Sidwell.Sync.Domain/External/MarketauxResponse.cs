using System.Text.Json.Serialization;

namespace Sidwell.Sync.Domain.External;

public sealed record MarketauxResponse(
    [property: JsonPropertyName("data")] IReadOnlyList<MarketauxArticle>? Data,
    [property: JsonPropertyName("error")] MarketauxError? Error);

public sealed record MarketauxArticle(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("published_at")] string? PublishedAt,
    [property: JsonPropertyName("source")] string? Source,
    [property: JsonPropertyName("entities")] IReadOnlyList<MarketauxEntity>? Entities);

public sealed record MarketauxEntity(
    [property: JsonPropertyName("symbol")] string? Symbol,
    [property: JsonPropertyName("sentiment_score")] decimal SentimentScore);

public sealed record MarketauxError(
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("message")] string? Message);
