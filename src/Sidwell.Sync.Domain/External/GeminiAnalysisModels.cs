using System.Text.Json.Serialization;

namespace Sidwell.Sync.Domain.External;

public sealed record SentimentResult(
    [property: JsonPropertyName("sentiment_score")] double SentimentScore,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("key_themes")] IReadOnlyList<string>? KeyThemes,
    [property: JsonPropertyName("risk_factors")] IReadOnlyList<string>? RiskFactors,
    [property: JsonPropertyName("catalysts")] IReadOnlyList<string>? Catalysts);

public sealed record Synthesis(
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("worth_score")] double WorthScore,
    [property: JsonPropertyName("estimates")] SynthesisEstimates? Estimates,
    [property: JsonPropertyName("composite_score")] double CompositeScore,
    [property: JsonPropertyName("direction")] string? Direction,
    [property: JsonPropertyName("critical_alert")] CriticalAlert? CriticalAlert);

public sealed record SynthesisEstimates(
    [property: JsonPropertyName("low")] double Low,
    [property: JsonPropertyName("high")] double High);

public sealed record CriticalAlert(
    [property: JsonPropertyName("is_critical")] bool IsCritical,
    [property: JsonPropertyName("severity")] string? Severity,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("message")] string? Message);
