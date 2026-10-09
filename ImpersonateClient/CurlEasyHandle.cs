namespace ImpersonateClient;

internal sealed class CurlEasyHandle : IDisposable
{
    private nint _headers;

    public nint Easy { get; }

    private CurlEasyHandle(nint easy)
    {
        Easy = easy;
    }

    public static CurlEasyHandle Create()
    {
        nint easy = NativeMethods.CurlEasyInit();

        if (easy == 0)
        {
            throw new InvalidOperationException("curl_easy_init failed.");
        }

        return new CurlEasyHandle(easy);
    }

    public void AppendHeader(string header)
    {
        _headers = NativeMethods.CurlSlistAppend(_headers, header);

        if (_headers == 0)
        {
            throw new InvalidOperationException("curl_slist_append failed.");
        }
    }

    public void ApplyHeaders()
    {
        if (_headers != 0)
        {
            Curl.SetPointer(Easy, CurlOption.HttpHeader, _headers);
        }
    }

    public void Dispose()
    {
        if (_headers != 0)
        {
            NativeMethods.CurlSlistFreeAll(_headers);
            _headers = 0;
        }

        if (Easy != 0)
        {
            NativeMethods.CurlEasyCleanup(Easy);
        }
    }
}