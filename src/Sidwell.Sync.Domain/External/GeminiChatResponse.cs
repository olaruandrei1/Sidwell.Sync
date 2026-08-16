using System.Text.Json.Serialization;

namespace Sidwell.Sync.Domain.External;

public sealed record GeminiGenerateResponse(
    [property: JsonPropertyName("candidates")] IReadOnlyList<GeminiCandidate>? Candidates);

public sealed record GeminiCandidate(
    [property: JsonPropertyName("content")] GeminiContent? Content);

public sealed record GeminiContent(
    [property: JsonPropertyName("parts")] IReadOnlyList<GeminiPart>? Parts);

public sealed record GeminiPart(
    [property: JsonPropertyName("text")] string? Text);
