using System.Net;
using System.Text;
using System.Text.Json;

namespace ImpersonateClient;

public sealed class ImpersonateHttpClient
{
    internal Browser? Browser { get; }
    internal bool PermuteExtensions { get; }
    internal bool DefaultRequestHeaders { get; }
    internal bool FollowRedirects { get; }
    internal bool Verify { get; }
    internal TimeSpan? Timeout { get; }
    internal string? Proxy { get; }

    private ImpersonateHttpClient(ImpersonateHttpClientOptions options)
    {
        Browser = options.Browser;
        PermuteExtensions = options.PermuteExtensions;
        DefaultRequestHeaders = options.DefaultRequestHeaders;
        FollowRedirects = options.FollowRedirects;
        Verify = options.Verify;
        Timeout = options.Timeout;
        Proxy = options.Proxy;
    }

    public static ImpersonateHttpClientBuilder Builder()
    {
        return new ImpersonateHttpClientBuilder();
    }

    public static ImpersonateHttpClient CreateDefault()
    {
        return Builder().Build();
    }

    public RequestBuilder Request(string url)
    {
        return new RequestBuilder(this, url);
    }

    public Task<ImpersonateHttpResponseMessage> GetAsync(string url,
                                                                  CancellationToken cancellationToken = default)
    {
        var request = Request(url)
                     .WithMethod("GET");
        return Task.FromResult(request.Send());
    }
    
    public Task<ImpersonateHttpResponseMessage> PostAsJsonAsync<T>(string url, T value,
                                                                   CancellationToken cancellationToken = default)
    {
        var request = Request(url)
                     .WithMethod("POST")
                     .WithJsonBody(value);
        return Task.FromResult(request.Send());
    }

    internal ImpersonateHttpClientOptions ToOptions()
    {
        return new ImpersonateHttpClientOptions
        {
            Browser = Browser,
            PermuteExtensions = PermuteExtensions,
            DefaultRequestHeaders = DefaultRequestHeaders,
            FollowRedirects = FollowRedirects,
            Verify = Verify,
            Timeout = Timeout,
            Proxy = Proxy,
        };
    }

    internal static ImpersonateHttpClient FromOptions(ImpersonateHttpClientOptions options)
    {
        return new ImpersonateHttpClient(options);
    }
}

internal sealed class ImpersonateHttpClientOptions
{
    public Browser? Browser { get; init; }
    public bool PermuteExtensions { get; init; } = true;
    public bool DefaultRequestHeaders { get; init; } = true;
    public bool FollowRedirects { get; init; } = true;
    public bool Verify { get; init; } = true;
    public TimeSpan? Timeout { get; init; }
    public string? Proxy { get; init; }
}

public sealed class ImpersonateHttpClientBuilder
{
    private Browser? _browser;
    private bool _permuteExtensions = true;
    private bool _defaultHeaders = true;
    private bool _followRedirects = true;
    private bool _verify = true;
    private TimeSpan? _timeout;
    private string? _proxy;

    internal ImpersonateHttpClientBuilder()
    {
    }

    public ImpersonateHttpClientBuilder WithBrowser(Browser browser)
    {
        _browser = browser;
        return this;
    }

    /// <summary>Not supported yet: always throws.</summary>
    /// <remarks>
    ///     Custom JA3 fingerprints need parsing into curl-impersonate's TLS options (cipher list, signature
    ///     algorithms, extension order, GREASE, TLS version). Until that exists, use a <see cref="Browser"/> preset.
    /// </remarks>
    /// <exception cref="NotSupportedException">Always.</exception>
    public ImpersonateHttpClientBuilder WithJa3(string ja3)
    {
        throw new NotSupportedException("Custom JA3 fingerprints aren't implemented yet; use a Browser preset instead.");
    }

    /// <summary>Not supported yet: always throws.</summary>
    /// <remarks>
    ///     Custom Akamai HTTP/2 fingerprints need parsing into curl-impersonate's HTTP/2 options (settings, window
    ///     update, stream priority, pseudo-header order). Until that exists, use a <see cref="Browser"/> preset.
    /// </remarks>
    /// <exception cref="NotSupportedException">Always.</exception>
    public ImpersonateHttpClientBuilder WithAkamai(string akamai)
    {
        throw new NotSupportedException("Custom Akamai HTTP/2 fingerprints aren't implemented yet; use a Browser preset instead.");
    }

    public ImpersonateHttpClientBuilder WithPermuteExtensions(bool enabled = true)
    {
        _permuteExtensions = enabled;
        return this;
    }

    public ImpersonateHttpClientBuilder WithDefaultHeaders(bool enabled = true)
    {
        _defaultHeaders = enabled;
        return this;
    }

    public ImpersonateHttpClientBuilder WithFollowRedirects(bool enabled = true)
    {
        _followRedirects = enabled;
        return this;
    }

    public ImpersonateHttpClientBuilder WithVerify(bool enabled = true)
    {
        _verify = enabled;
        return this;
    }

    public ImpersonateHttpClientBuilder WithTimeout(TimeSpan timeout)
    {
        _timeout = timeout;
        return this;
    }

    public ImpersonateHttpClientBuilder WithProxy(string proxy)
    {
        _proxy = proxy;
        return this;
    }

    public ImpersonateHttpClient Build()
    {
        return ImpersonateHttpClient.FromOptions(new ImpersonateHttpClientOptions
        {
            Browser = _browser,
            PermuteExtensions = _permuteExtensions,
            DefaultRequestHeaders = _defaultHeaders,
            FollowRedirects = _followRedirects,
            Verify = _verify,
            Timeout = _timeout,
            Proxy = _proxy,
        });
    }
}