using System.Net;
using System.Text;
using System.Text.Json;

namespace ImpersonateClient;

public sealed class ImpersonateHttpClient
{
    internal Browser? Browser { get; }
    internal string? Ja3 { get; }
    internal string? Akamai { get; }
    internal bool PermuteExtensions { get; }
    internal bool DefaultHeaders { get; }
    internal bool FollowRedirects { get; }
    internal bool Verify { get; }
    internal TimeSpan? Timeout { get; }
    internal string? Proxy { get; }

    private ImpersonateHttpClient(ImpersonateHttpClientOptions options)
    {
        Browser = options.Browser;
        Ja3 = options.Ja3;
        Akamai = options.Akamai;
        PermuteExtensions = options.PermuteExtensions;
        DefaultHeaders = options.DefaultHeaders;
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

    internal ImpersonateHttpClientOptions ToOptions()
    {
        return new ImpersonateHttpClientOptions
        {
            Browser = Browser,
            Ja3 = Ja3,
            Akamai = Akamai,
            PermuteExtensions = PermuteExtensions,
            DefaultHeaders = DefaultHeaders,
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
    public string? Ja3 { get; init; }
    public string? Akamai { get; init; }
    public bool PermuteExtensions { get; init; } = true;
    public bool DefaultHeaders { get; init; } = true;
    public bool FollowRedirects { get; init; } = true;
    public bool Verify { get; init; } = true;
    public TimeSpan? Timeout { get; init; }
    public string? Proxy { get; init; }
}

public sealed class ImpersonateHttpClientBuilder
{
    private Browser? _browser;
    private string? _ja3;
    private string? _akamai;
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

    public ImpersonateHttpClientBuilder WithJa3(string ja3)
    {
        _ja3 = ja3;
        return this;
    }

    public ImpersonateHttpClientBuilder WithAkamai(string akamai)
    {
        _akamai = akamai;
        return this;
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
            Ja3 = _ja3,
            Akamai = _akamai,
            PermuteExtensions = _permuteExtensions,
            DefaultHeaders = _defaultHeaders,
            FollowRedirects = _followRedirects,
            Verify = _verify,
            Timeout = _timeout,
            Proxy = _proxy,
        });
    }
}

public sealed class RequestBuilder
{
    private readonly ImpersonateHttpClient _client;
    private readonly string _url;
    private readonly List<string> _headers = [];

    private string _method = "GET";
    private byte[]? _body;
    private Browser? _browserOverride;
    private string? _ja3Override;
    private string? _akamaiOverride;
    private bool? _permuteExtensionsOverride;
    private bool? _defaultHeadersOverride;
    private bool? _followRedirectsOverride;
    private bool? _verifyOverride;
    private TimeSpan? _timeoutOverride;
    private string? _proxyOverride;
    private (string User, string Password)? _auth;

    public RequestBuilder(string url)
        : this(ImpersonateHttpClient.CreateDefault(), url)
    {
    }

    internal RequestBuilder(ImpersonateHttpClient client, string url)
    {
        _client = client;
        _url = url;
    }

    public RequestBuilder WithMethod(string method)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            throw new ArgumentException("Method cannot be empty.", nameof(method));
        }

        _method = method.Trim().ToUpperInvariant();
        return this;
    }

    public RequestBuilder WithHeader(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Header key cannot be empty.", nameof(key));
        }

        _headers.Add($"{key}: {value}");
        return this;
    }

    public RequestBuilder WithBody(byte[] bytes)
    {
        _body = bytes;
        return this;
    }

    public RequestBuilder WithBody(string body, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;
        _body = encoding.GetBytes(body);
        return this;
    }

    public RequestBuilder WithJsonBody<T>(T data)
    {
        _body = JsonSerializer.SerializeToUtf8Bytes(data);
        return WithHeader("Content-Type", "application/json");
    }

    public RequestBuilder WithFormBody(IReadOnlyDictionary<string, string> data)
    {
        string form = string.Join(
            "&",
            data.Select(pair =>
                $"{WebUtility.UrlEncode(pair.Key)}={WebUtility.UrlEncode(pair.Value)}"));

        _body = Encoding.UTF8.GetBytes(form);
        return WithHeader("Content-Type", "application/x-www-form-urlencoded");
    }

    public RequestBuilder WithBasicAuth(string user, string password)
    {
        _auth = (user, password);
        return this;
    }

    public RequestBuilder WithImpersonation(Browser browser)
    {
        _browserOverride = browser;
        return this;
    }

    public RequestBuilder WithJa3(string ja3)
    {
        _ja3Override = ja3;
        return this;
    }

    public RequestBuilder WithAkamai(string akamai)
    {
        _akamaiOverride = akamai;
        return this;
    }

    public RequestBuilder WithPermuteExtensions(bool enabled = true)
    {
        _permuteExtensionsOverride = enabled;
        return this;
    }

    public RequestBuilder WithDefaultHeaders(bool enabled = true)
    {
        _defaultHeadersOverride = enabled;
        return this;
    }

    public RequestBuilder WithFollowRedirects(bool enabled = true)
    {
        _followRedirectsOverride = enabled;
        return this;
    }

    public RequestBuilder WithVerify(bool enabled = true)
    {
        _verifyOverride = enabled;
        return this;
    }

    public RequestBuilder WithTimeout(TimeSpan timeout)
    {
        _timeoutOverride = timeout;
        return this;
    }

    public RequestBuilder WithProxy(string proxy)
    {
        _proxyOverride = proxy;
        return this;
    }

    public ImpersonateHttpResponseMessage Send()
    {
        unsafe
        {
            var request = Resolve();

            using var handle = CurlEasyHandle.Create();

            ConfigureBasicOptions(handle.Easy, request);
            ConfigureMethodAndBody(handle.Easy, request);
            ConfigureHeaders(handle, request);
            ConfigureImpersonation(handle.Easy, request);

            var capture = new CurlResponseCapture();

            NativeMethods.CurlWriteCallback bodyCallback = capture.WriteBody;
            NativeMethods.CurlWriteCallback headerCallback = capture.WriteHeader;

            Curl.SetWriteCallback(handle.Easy, CurlOption.WriteFunction, bodyCallback);
            Curl.SetWriteCallback(handle.Easy, CurlOption.HeaderFunction, headerCallback);

            Curl.ThrowIfError(
                NativeMethods.CurlEasyPerform(handle.Easy),
                "curl_easy_perform");

            GC.KeepAlive(bodyCallback);
            GC.KeepAlive(headerCallback);
            GC.KeepAlive(capture);

            var parsedHeaders = HeaderParser.Parse(capture.Headers.ToArray());

            var statusCode = Curl.GetResponseCode(handle.Easy);

            return new ImpersonateHttpResponseMessage
            {
                StatusCode = checked((int)statusCode),
                ReasonPhrase = parsedHeaders.ReasonPhrase,
                Version = parsedHeaders.Version,
                RequestUri = new Uri(request.Url),
                Content = capture.Body.ToArray(),
                Headers = parsedHeaders.Headers, // this needs init/set support
            };
        }
    }

    private ResolvedRequest Resolve()
    {
        return new ResolvedRequest
        {
            Url = _url,
            Method = _method,
            Headers = _headers.ToArray(),
            Body = _body,

            Browser = _browserOverride ?? _client.Browser,
            Ja3 = _ja3Override ?? _client.Ja3,
            Akamai = _akamaiOverride ?? _client.Akamai,
            PermuteExtensions = _permuteExtensionsOverride ?? _client.PermuteExtensions,
            DefaultHeaders = _defaultHeadersOverride ?? _client.DefaultHeaders,
            FollowRedirects = _followRedirectsOverride ?? _client.FollowRedirects,
            Verify = _verifyOverride ?? _client.Verify,
            Timeout = _timeoutOverride ?? _client.Timeout,
            Proxy = _proxyOverride ?? _client.Proxy,
            Auth = _auth,
        };
    }

    private static void ConfigureBasicOptions(nint easy, ResolvedRequest request)
    {
        Curl.SetString(easy, CurlOption.Url, request.Url);
        Curl.SetString(easy, CurlOption.AcceptEncoding, "");
        Curl.SetLong(easy, CurlOption.SslVerifyPeer, request.Verify ? 1 : 0);
        Curl.SetLong(easy, CurlOption.SslVerifyHost, request.Verify ? 2 : 0);
        Curl.SetString(easy, CurlOption.CaInfo, Path.Combine(AppContext.BaseDirectory, "cacert.pem"));
        Curl.SetLong(easy, CurlOption.FollowLocation, request.FollowRedirects ? 1 : 0);
        Curl.SetLong(easy, CurlOption.Timeout, request.Timeout is null ? 0 : (long)request.Timeout.Value.TotalSeconds);
        Curl.SetString(easy, CurlOption.Proxy, request.Proxy ?? "");

        if (request.Auth is { } auth)
        {
            Curl.SetString(easy, CurlOption.Username, auth.User);
            Curl.SetString(easy, CurlOption.Password, auth.Password);
        }
        else
        {
            Curl.SetString(easy, CurlOption.Username, "");
            Curl.SetString(easy, CurlOption.Password, "");
        }
    }

    private static void ConfigureMethodAndBody(nint easy, ResolvedRequest request)
    {
        switch (request.Method)
        {
            case "GET":
            {
                Curl.SetLong(easy, CurlOption.HttpGet, 1);
                break;
            }

            case "POST":
            {
                Curl.SetLong(easy, CurlOption.Post, 1);
                SetRequestBodyIfPresent(easy, request.Body);
                break;
            }

            case "PUT":
            {
                Curl.SetString(easy, CurlOption.CustomRequest, "PUT");
                SetRequestBodyIfPresent(easy, request.Body);
                break;
            }

            case "HEAD":
            {
                Curl.SetLong(easy, CurlOption.Nobody, 1);
                break;
            }

            default:
            {
                Curl.SetString(easy, CurlOption.CustomRequest, request.Method);
                SetRequestBodyIfPresent(easy, request.Body);
                break;
            }
        }
    }

    private static unsafe void SetRequestBodyIfPresent(nint easy, byte[]? body)
    {
        if (body is null)
        {
            return;
        }

        Curl.SetLong(easy, CurlOption.PostFieldSizeLarge, body.LongLength);

        fixed (byte* bodyPointer = body)
        {
            Curl.SetPointer(easy, CurlOption.CopyPostFields, (nint)bodyPointer);
        }
    }

    private static void ConfigureHeaders(CurlEasyHandle handle, ResolvedRequest request)
    {
        foreach (string header in request.Headers)
        {
            handle.AppendHeader(header);
        }

        handle.ApplyHeaders();
    }

    private static void ConfigureImpersonation(nint easy, ResolvedRequest request)
    {
        // Priority:
        // 1. Explicit JA3/Akamai custom fingerprint overrides.
        // 2. Browser profile preset.

        if (request.Browser is { } browser)
        {
            Curl.ThrowIfError(
                NativeMethods.CurlEasyImpersonate(
                    easy,
                    browser.ToBrowserString(),
                    request.DefaultHeaders ? 1 : 0),
                $"curl_easy_impersonate({browser})");
        }

        if (!string.IsNullOrWhiteSpace(request.Ja3))
        {
            ApplyJa3Placeholder(easy, request.Ja3, request.PermuteExtensions);
        }

        if (!string.IsNullOrWhiteSpace(request.Akamai))
        {
            ApplyAkamaiPlaceholder(easy, request.Akamai);
        }
    }

    private static void ApplyJa3Placeholder(nint easy, string ja3, bool permuteExtensions)
    {
        _ = easy;
        _ = ja3;
        _ = permuteExtensions;

        // TODO:
        // Parse JA3 and set the corresponding curl-impersonate options.
        //
        // Expected future mappings may include:
        // - CurlOption.SslCipherList
        // - CurlOption.SslSigHashAlgs
        // - CurlOption.TlsExtensionOrder
        // - CurlOption.SslPermuteExtensions
        // - CurlOption.TlsGrease
        // - CurlOption.SslVersion
    }

    private static void ApplyAkamaiPlaceholder(nint easy, string akamai)
    {
        _ = easy;
        _ = akamai;

        // TODO:
        // Parse Akamai HTTP/2 fingerprint and set the corresponding curl-impersonate options.
        //
        // Expected future mappings may include:
        // - CurlOption.Http2Settings
        // - CurlOption.Http2WindowUpdate
        // - CurlOption.Http2Streams
        // - CurlOption.Http2PseudoHeadersOrder
        // - CurlOption.Http2NoPriority
        // - CurlOption.StreamWeight
        // - CurlOption.StreamExclusive
    }
}

internal sealed class ResolvedRequest
{
    public required string Url { get; init; }
    public required string Method { get; init; }
    public required IReadOnlyList<string> Headers { get; init; }
    public byte[]? Body { get; init; }

    public Browser? Browser { get; init; }
    public string? Ja3 { get; init; }
    public string? Akamai { get; init; }
    public bool PermuteExtensions { get; init; }
    public bool DefaultHeaders { get; init; }
    public bool FollowRedirects { get; init; }
    public bool Verify { get; init; }
    public TimeSpan? Timeout { get; init; }
    public string? Proxy { get; init; }
    public (string User, string Password)? Auth { get; init; }
}