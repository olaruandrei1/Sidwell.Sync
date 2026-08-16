namespace Sidwell.Sync.Domain.Models;

public sealed record NewsArticle(string Title, string Url, DateTimeOffset PublishedAt, decimal? Sentiment, string? Source = null);
