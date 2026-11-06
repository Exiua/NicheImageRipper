# MegaSdk

C# bindings for the official [MEGA SDK](https://github.com/meganz/sdk). It covers what's needed to rip from MEGA:
public file links, folder links, account login, browsing nodes, and downloads with progress and cancellation.

The SDK only has a C++ API, so `native/` contains a small `extern "C"` shim (`megasdk_c.h`) over `MegaApi` that's
built into a single `megasdk` library, and this project calls it with `[LibraryImport]`.

## Usage

```csharp
using var client = new MegaClient();

// Public file link
using var file = await client.GetPublicNodeAsync("https://mega.nz/file/<id>#<key>");
await client.DownloadAsync(file, @"C:\Downloads\", new Progress<MegaTransferProgress>(p =>
    Console.WriteLine($"{p.TransferredBytes}/{p.TotalBytes}")));

// Folder link (a client logged in to a folder link only sees that folder)
using var folderClient = new MegaClient();
using var root = await folderClient.OpenFolderLinkAsync("https://mega.nz/folder/<id>#<key>");
foreach (var (path, node) in folderClient.EnumerateDescendants(root))
{
    Console.WriteLine($"{path} {node.Size}");
}

await folderClient.DownloadAsync(root, @"C:\Downloads\"); // downloads the whole folder
```

No app key is needed (the SDK ignores it). `MegaClientOptions` sets the cache folder and user agent, and
`MegaLogging` forwards the SDK's log output.

Callbacks from the SDK run on its threads. Tasks complete asynchronously, so continuations don't run on them.
Requests (login, fetch nodes, public links) can't be cancelled in the SDK: cancelling only stops the wait. Downloads
are cancelled in the SDK.

## Native Library

The native library isn't committed. Build it from `native/` against the MEGA SDK, which is the `native/sdk`
submodule (pinned to a release, currently v10.22.0). The SDK's dependencies come from
[vcpkg](https://github.com/microsoft/vcpkg), using the SDK's own `vcpkg.json`. Features that aren't needed for
downloading (sync, chat, thumbnails, media metadata) are turned off, so FreeImage, FFmpeg, PDFium and so on aren't
built.

vcpkg builds ICU with autotools, which can't handle `#` or spaces in paths, so neither vcpkg nor the build
directory (vcpkg installs packages into it) can live inside this repository (`C#` is in its path). Use plain paths,
e.g. `C:\dev\vcpkg` and `C:\dev\build-megasdk`:

```powershell
git submodule update --init lib/MegaSdk/native/sdk
git clone https://github.com/microsoft/vcpkg C:\dev\vcpkg
# Check out the SDK's builtin-baseline (lib/MegaSdk/native/sdk/vcpkg.json), which its overlay ports are written
# against. It's ef7dbf94b9198bc58f45951adcf1f041fcbc5ea0 for v10.22.0.
git -C C:\dev\vcpkg checkout ef7dbf94b9198bc58f45951adcf1f041fcbc5ea0

cmake -S lib\MegaSdk\native -B C:\dev\build-megasdk -G "Visual Studio 17 2022" -A x64 -DVCPKG_ROOT=C:/dev/vcpkg
cmake --build C:\dev\build-megasdk --config Release --target megasdk
```

To move to a newer SDK release, check out its tag in the submodule, update the vcpkg checkout to its
`builtin-baseline`, rebuild, and commit the submodule change.

On some Windows machines, vcpkg's MSYS2 (used to run ICU's autotools build) can't start child processes: the build
fails with exit code 127, or MSYS2 logs `fork: Resource temporarily unavailable` / `address space needed by ... is
already occupied`. That's Exploit Protection's Mandatory ASLR ("Force randomization for images") relocating MSYS2's
DLLs. Exempt the MSYS2 executables vcpkg uses, as is usually already done for `C:\msys64` and Git's MSYS2:

```powershell
# Elevated. vcpkg extracts MSYS2 to <downloads>/tools/msys2/<hash>/ (set VCPKG_DOWNLOADS to keep it in one place).
Get-ChildItem <downloads>\tools\msys2\*\usr\bin\*.exe | ForEach-Object { Set-ProcessMitigation -Name $_.FullName -Disable ForceRelocateImages }
```

If that isn't enough, replace the contents of the MSYS2 directory named in the build output with a copy of a
working MSYS2 install's `usr` and `etc` (with autoconf, automake, libtool, pkgconf and autoconf-archive installed via
pacman, whose DLLs are already rebased), then exempt its executables as above. vcpkg reuses an existing directory.
Don't link it to the real install instead: vcpkg deletes `usr/lib/pkgconfig` and similar directories in it.

The first configure builds the vcpkg dependencies (ICU, curl, OpenSSL, Crypto++, libsodium, SQLite), which takes a
while. Dependencies are linked statically, and the built `megasdk` (`.dll`, `.so` or `.dylib`) is copied to
`runtimes/<rid>/native/`, which is copied to the output and loaded from there. Set `MEGA_SDK_DIR` to use an SDK
checkout elsewhere, and `MEGASDK_RID` to override the runtime identifier.

On Windows, `native/cmake/vcpkg_overlay_triplets` overrides the SDK's `x64-windows-mega` triplet to use the VS 2022
toolset (the SDK's triplet pins VS 2019). On Linux and macOS, use `-G "Unix Makefiles" -DCMAKE_BUILD_TYPE=Release`.
