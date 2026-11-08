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

The native library isn't committed. Put `libcurl-impersonate` (`.dll`, `.so` or `.dylib`) in `runtimes/<rid>/native/`,
for example `runtimes/win-x64/native/`. It's copied to the output and loaded from there. A CA bundle
(`Resources/cacert.pem`) is embedded for certificate checks.

curl-impersonate is the `native/curl-impersonate` submodule, pinned to a release (currently v2.3.2). The release
workflow builds it for each platform with curl-impersonate's own scripts. Its dependencies are linked statically, so
the library is a single file. To build it locally, from the submodule (`git submodule update --init
lib/ImpersonateClient/native/curl-impersonate`):

```powershell
# Windows (Visual Studio 2022 with the C++ and Clang tools, NASM on PATH); output: packages\lib\libcurl-impersonate*.dll
win\build.bat vcvars64
```

```bash
# Linux/macOS (cmake, ninja, pkg-config; use gmake on macOS); output: packages/lib/libcurl-impersonate.{so,dylib}
make configure build install-strip BUILD_DIR=build CMAKE_CONFIGURE_ARGS="-G Ninja -DCMAKE_INSTALL_PREFIX=$PWD/packages -DUSE_LIBIDN2=OFF"
```

Copy the library to `runtimes/<rid>/native/libcurl-impersonate.<ext>` (the build output's versioned names don't
matter). The `Browser` targets must exist in the pinned version: see `bin/` in the submodule.
