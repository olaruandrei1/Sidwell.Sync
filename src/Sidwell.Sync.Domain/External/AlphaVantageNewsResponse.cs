using System.Text.Json.Serialization;

namespace Sidwell.Sync.Domain.External;

public sealed record AlphaVantageNewsResponse(
    [property: JsonPropertyName("feed")] IReadOnlyList<AlphaVantageArticle>? Feed,
    [property: JsonPropertyName("Error Message")] string? ErrorMessage,
    [property: JsonPropertyName("Information")] string? Information,
    [property: JsonPropertyName("Note")] string? Note);

public sealed record AlphaVantageArticle(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("time_published")] string? TimePublished,
    [property: JsonPropertyName("overall_sentiment_score")] decimal OverallSentimentScore,
    [property: JsonPropertyName("ticker_sentiment")] IReadOnlyList<AlphaVantageTickerSentiment>? TickerSentiment);

public sealed record AlphaVantageTickerSentiment(
    [property: JsonPropertyName("ticker")] string? Ticker,
    [property: JsonPropertyName("ticker_sentiment_score")] string? TickerSentimentScore);
