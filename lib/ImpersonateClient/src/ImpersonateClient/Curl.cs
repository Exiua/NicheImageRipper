namespace ImpersonateClient;

internal static class Curl
{
    public static void SetString(nint easy, CurlOption option, string value)
    {
        ThrowIfError(
            NativeMethods.CurlEasySetOptString(easy, option, value),
            $"curl_easy_setopt({option})");
    }

    public static void SetLong(nint easy, CurlOption option, long value)
    {
        ThrowIfError(
            NativeMethods.CurlEasySetOptLong(easy, option, value),
            $"curl_easy_setopt({option})");
    }

    public static void SetPointer(nint easy, CurlOption option, nint value)
    {
        ThrowIfError(
            NativeMethods.CurlEasySetOptPointer(easy, option, value),
            $"curl_easy_setopt({option})");
    }

    public static void SetWriteCallback(
        nint easy,
        CurlOption option,
        NativeMethods.CurlWriteCallback callback)
    {
        ThrowIfError(
            NativeMethods.CurlEasySetOptWriteCallback(easy, option, callback),
            $"curl_easy_setopt({option})");
    }

    public static long GetResponseCode(nint easy)
    {
        ThrowIfError(
            NativeMethods.CurlEasyGetInfoLong(
                easy,
                CurlInfo.ResponseCode,
                out var responseCode),
            "curl_easy_getinfo(CURLINFO_RESPONSE_CODE)");

        return responseCode;
    }

    public static void ThrowIfError(CurlCode code, string operation)
    {
        if (code != CurlCode.Ok)
        {
            throw new InvalidOperationException($"{operation} failed with CURLcode {code}.");
        }
    }
}