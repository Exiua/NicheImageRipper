using System.Reflection;

namespace NicheImageRipper.Sdk.Utility;

public static class AssemblyVersionUtility
{
    /// <summary>
    ///     Version from the &lt;Version&gt; element of the .csproj that built the assembly containing
    ///     <typeparamref name="T"/>, as Major.Minor.Patch.
    /// </summary>
    public static Version Of<T>() => Of(typeof(T).Assembly);

    /// <inheritdoc cref="Of{T}"/>
    public static Version Of(Assembly assembly)
    {
        // The SDK derives AssemblyVersion from <Version> (dropping any prerelease suffix) and pads it to four
        // parts; trim the revision so it displays as "5.1.0" and compares equal to release tags like "v5.1.0"
        var version = assembly.GetName().Version ?? new Version(0, 0, 0);
        return new Version(version.Major, version.Minor, Math.Max(version.Build, 0));
    }
}
