namespace ImpersonateClient;

using System.Reflection;
using System.Runtime.InteropServices;

internal static class NativeLibraryResolver
{
    public static void Register()
    {
        NativeLibrary.SetDllImportResolver(
            typeof(NativeMethods).Assembly,
            Resolve);
    }

    private static nint Resolve(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        var rid = GetRid();

        var extension = OperatingSystem.IsWindows()
            ? ".dll"
            : OperatingSystem.IsMacOS()
                ? ".dylib"
                : ".so";

        var fileName = libraryName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
            ? libraryName
            : $"{libraryName}{extension}";

        var path = Path.Combine(
            AppContext.BaseDirectory,
            "runtimes",
            rid,
            "native",
            fileName);

        if (File.Exists(path))
        {
            return NativeLibrary.Load(path, assembly, searchPath);
        }

        return 0;
    }

    private static string GetRid()
    {
        var os = OperatingSystem.IsWindows()
            ? "win"
            : OperatingSystem.IsMacOS()
                ? "osx"
                : "linux";

        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => throw new PlatformNotSupportedException(
                $"Unsupported architecture: {RuntimeInformation.ProcessArchitecture}."),
        };

        return $"{os}-{arch}";
    }
}