# ImpersonateClient

An HTTP client that impersonates real browsers' TLS and HTTP/2 fingerprints, for sites that block ordinary .NET
requests. It's a C# wrapper over [curl-impersonate](https://github.com/lexiforest/curl-impersonate)'s
`libcurl-impersonate`, in the spirit of Python's [curl_cffi](https://github.com/lexiforest/curl_cffi).

## Usage

```csharp
var client = ImpersonateHttpClient.Builder()
                                  .WithBrowser(Browser.Chrome136)
                                  .Build();
var response = await client.GetAsync("https://example.com/");
```

`Request(url)` returns a builder for custom methods, headers, and bodies. Custom JA3/Akamai fingerprints aren't
supported yet; use a `Browser` preset.

## Native Library

The native library isn't committed. Put `libcurl-impersonate` (`.dll`, `.so` or `.dylib`, plus its dependencies,
e.g. `zlib.dll` on Windows) in `runtimes/<rid>/native/`, for example `runtimes/win-x64/native/`. It's copied to the
output and loaded from there. A CA bundle (`Resources/cacert.pem`) is embedded for certificate checks.
