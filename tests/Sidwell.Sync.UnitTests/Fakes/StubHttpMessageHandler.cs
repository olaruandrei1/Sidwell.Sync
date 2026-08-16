using System.Net;
using System.Text;

namespace Sidwell.Sync.UnitTests.Fakes;

public sealed class StubHttpMessageHandler(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
{
    public Uri? LastRequestUri { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequestUri = request.RequestUri;
        return Task.FromResult(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
    }
}
