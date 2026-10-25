using System.Net;
using System.Text;
using System.Text.Json;

namespace ImpersonateClient;

public sealed class RequestBuilder
{
    private readonly ImpersonateHttpClient _client;
    private readonly string _url;
    private readonly List<string> _headers = [];

    private string _method = "GET";
    private byte[]? _body;
    private Browser? _browserOverride;
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
        var form = string.Join(
            "&",
            data.Select(pair =>
                $"{WebUtility.UrlEncode(pair.Key)}={WebUtility.UrlEncode(pair.Value)}"));

        _body = Encoding.UTF8.GetBytes(form);
        return WithHeader("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");
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

    /// <summary>Not supported yet: always throws.</summary>
    /// <remarks>
    ///     Custom JA3 fingerprints need parsing into curl-impersonate's TLS options (cipher list, signature
    ///     algorithms, extension order, GREASE, TLS version). Until that exists, use a <see cref="WithBrowser"/> preset.
    /// </remarks>
    /// <exception cref="NotSupportedException">Always.</exception>
    public RequestBuilder WithJa3(string ja3)
    {
        throw new NotSupportedException("Custom JA3 fingerprints aren't implemented yet; use a Browser preset instead.");
    }

    /// <summary>Not supported yet: always throws.</summary>
    /// <remarks>
    ///     Custom Akamai HTTP/2 fingerprints need parsing into curl-impersonate's HTTP/2 options (settings, window
    ///     update, stream priority, pseudo-header order). Until that exists, use a <see cref="WithBrowser"/> preset.
    /// </remarks>
    /// <exception cref="NotSupportedException">Always.</exception>
    public RequestBuilder WithAkamai(string akamai)
    {
        throw new NotSupportedException("Custom Akamai HTTP/2 fingerprints aren't implemented yet; use a Browser preset instead.");
    }

    /// <inheritdoc cref="ImpersonateHttpClientBuilder.WithPermuteExtensions"/>
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
            
            var stream = capture.Body;

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            HttpContent content = new StreamContent(stream);

            return new ImpersonateHttpResponseMessage
            {
                StatusCode = checked((HttpStatusCode)(int)statusCode),
                ReasonPhrase = parsedHeaders.ReasonPhrase,
                Version = parsedHeaders.Version,
                RequestUri = new Uri(request.Url),
                Content = content,
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
            PermuteExtensions = _permuteExtensionsOverride ?? _client.PermuteExtensions,
            DefaultHeaders = _defaultHeadersOverride ?? _client.DefaultRequestHeaders,
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
        Curl.SetString(easy, CurlOption.CaInfo, CurlCertificateBundle.ResolveCaInfoPath());
        Curl.SetLong(easy, CurlOption.FollowLocation, request.FollowRedirects ? 1 : 0);
        Curl.SetLong(easy, CurlOption.Timeout, request.Timeout is null ? 0 : (long)request.Timeout.Value.TotalSeconds);
        Curl.SetString(easy, CurlOption.Proxy, request.Proxy ?? "");

        if (request.Auth is { } auth)
        {
            Curl.SetString(easy, CurlOption.Username, auth.User);
            Curl.SetString(easy, CurlOption.Password, auth.Password);
        }
        /*else
        {
            Curl.SetString(easy, CurlOption.Username, "");
            Curl.SetString(easy, CurlOption.Password, "");
        }*/
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
        foreach (var header in request.Headers)
        {
            handle.AppendHeader(header);
        }

        handle.ApplyHeaders();
    }

    private static void ConfigureImpersonation(nint easy, ResolvedRequest request)
    {
        // Browser profile preset. Custom JA3/Akamai fingerprints aren't supported yet (see WithJa3/WithAkamai).
        if (request.Browser is { } browser)
        {
            Curl.ThrowIfError(
                NativeMethods.CurlEasyImpersonate(
                    easy,
                    browser.ToBrowserString(),
                    request.DefaultHeaders ? 1 : 0),
                $"curl_easy_impersonate({browser})");
        }

        // After the preset, so an explicit choice overrides what the preset set
        if (request.PermuteExtensions is { } permute)
        {
            Curl.SetLong(easy, CurlOption.SslPermuteExtensions, permute ? 1 : 0);
        }
    }
}