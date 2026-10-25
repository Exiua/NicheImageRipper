using System.Runtime.InteropServices;

namespace ImpersonateClient;

internal static partial class NativeMethods
{
    private const string LibraryName = "libcurl-impersonate";

    static NativeMethods()
    {
        NativeLibraryResolver.Register();
    }
    
    [LibraryImport(
        LibraryName,
        EntryPoint = "curl_easy_impersonate",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial CurlCode CurlEasyImpersonate(
        nint curl,
        string target,
        int defaultHeaders);

    [LibraryImport(LibraryName, EntryPoint = "curl_easy_init")]
    internal static partial nint CurlEasyInit();

    [LibraryImport(LibraryName, EntryPoint = "curl_easy_cleanup")]
    internal static partial void CurlEasyCleanup(nint curl);

    [LibraryImport(LibraryName, EntryPoint = "curl_easy_perform")]
    internal static partial CurlCode CurlEasyPerform(nint curl);

    [LibraryImport(
        LibraryName,
        EntryPoint = "curl_easy_setopt",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial CurlCode CurlEasySetOptString(
        nint curl,
        CurlOption option,
        string value);

    [LibraryImport(LibraryName, EntryPoint = "curl_easy_setopt")]
    internal static partial CurlCode CurlEasySetOptLong(
        nint curl,
        CurlOption option,
        long value);

    [LibraryImport(LibraryName, EntryPoint = "curl_easy_setopt")]
    internal static partial CurlCode CurlEasySetOptPointer(
        nint curl,
        CurlOption option,
        nint value);

    [LibraryImport(
        LibraryName,
        EntryPoint = "curl_slist_append",
        StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint CurlSlistAppend(nint list, string value);

    [LibraryImport(LibraryName, EntryPoint = "curl_slist_free_all")]
    internal static partial void CurlSlistFreeAll(nint list);
    
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate nuint CurlWriteCallback(
        byte* buffer,
        nuint size,
        nuint nitems,
        nint userdata);
    
    [LibraryImport(LibraryName, EntryPoint = "curl_easy_setopt")]
    internal static partial CurlCode CurlEasySetOptWriteCallback(
        nint curl,
        CurlOption option,
        CurlWriteCallback callback);

    [LibraryImport(LibraryName, EntryPoint = "curl_easy_getinfo")]
    internal static partial CurlCode CurlEasyGetInfoLong(
        nint curl,
        CurlInfo info,
        out long value);
}