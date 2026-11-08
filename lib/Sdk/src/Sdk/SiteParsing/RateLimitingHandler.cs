namespace NicheImageRipper.Sdk.SiteParsing;

/// <summary>
///     Waits on <see cref="DomainRateLimiter"/> before sending each request. Sits outside the inner handler's redirect
///     handling, so only the first hop of a redirect chain counts against a budget.
/// </summary>
public sealed class RateLimitingHandler : DelegatingHandler
{
    public RateLimitingHandler() : base(new HttpClientHandler())
    {
    }

    public RateLimitingHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }

    /// <summary>An <see cref="HttpClient"/> whose requests are rate limited, optionally wrapping a configured handler.</summary>
    public static HttpClient CreateClient(HttpMessageHandler? innerHandler = null) =>
        new(innerHandler is null ? new RateLimitingHandler() : new RateLimitingHandler(innerHandler));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                                                                 CancellationToken cancellationToken)
    {
        if (request.RequestUri is { IsAbsoluteUri: true } uri)
        {
            await DomainRateLimiter.WaitAsync(uri, cancellationToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
