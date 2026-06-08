namespace AdOpsAgenReviewBanner.Tests.Support;

internal sealed class CapturingHttpMessageHandler : HttpMessageHandler
{
    private readonly List<HttpRequestMessage> _requests = [];

    public IReadOnlyList<HttpRequestMessage> Requests => _requests;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        _requests.Add(request);
        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}")
        });
    }
}
