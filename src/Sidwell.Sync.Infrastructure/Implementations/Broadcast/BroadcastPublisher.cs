using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sidwell.Sync.Application.Contracts.Infrastructure;
using Sidwell.Sync.Domain.ConfigurableObjects;

namespace Sidwell.Sync.Infrastructure.Implementations.Broadcast;

public sealed class BroadcastPublisher(
    IHttpClientFactory httpClientFactory,
    IOptions<BroadcastOptions> options,
    ILogger<BroadcastPublisher> logger
) : IBroadcastPublisher
{
    public const string HttpClientName = "broadcast";

    private readonly BroadcastOptions _options = options.Value;

    public async Task PublishAsync(string eventName, Guid? userId, object payload, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            return;

        try
        {
            HttpClient client = httpClientFactory.CreateClient(HttpClientName);

            var body = new { @event = eventName, userId = userId?.ToString(), payload };

            using var request = new HttpRequestMessage(HttpMethod.Post, "internal/broadcast")
            {
                Content = JsonContent.Create(body),
            };
            request.Headers.TryAddWithoutValidation("X-Internal-Secret", _options.Secret);

            await client.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Broadcast publish failed for {Event} (fire-and-forget)", eventName);
        }
    }
}
