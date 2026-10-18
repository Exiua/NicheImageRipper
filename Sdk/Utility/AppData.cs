namespace NicheImageRipper.Sdk.Utility;

/// <summary>
///     The one folder holding everything the app keeps between runs: config, history database, saved queues, logs,
///     cookie and credential files. Paths given to these helpers are relative to it, so code never depends on the
///     current working directory or needs to know where the folder is.
/// </summary>
/// <remarks>
///     Defaults to the executable's folder. Set the <c>NIR_DATA_DIR</c> environment variable to use another folder
///     (a relative value resolves against the current directory), or assign <see cref="Directory"/> at startup,
///     before anything reads a file.
/// </remarks>
public static class AppData
{
    public const string DirectoryVariable = "NIR_DATA_DIR";

    public static string Directory
    {
        get;
        set => field = System.IO.Path.GetFullPath(value);
    } = ResolveDefaultDirectory();

    private static string ResolveDefaultDirectory()
    {
        var overridden = Environment.GetEnvironmentVariable(DirectoryVariable);
        return System.IO.Path.GetFullPath(string.IsNullOrWhiteSpace(overridden) ? AppContext.BaseDirectory : overridden);
    }

    /// <summary>Full path of a file or folder in the data folder, e.g. <c>GetPath("Logs", "app.log")</c>.</summary>
    public static string GetPath(params string[] relativeParts) =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine([Directory, ..relativeParts]));

    /// <summary>Like <see cref="GetPath"/>, creating the folder if it doesn't exist.</summary>
    public static string EnsureDirectory(params string[] relativeParts)
    {
        var path = GetPath(relativeParts);
        System.IO.Directory.CreateDirectory(path);
        return path;
    }

    public static bool Exists(string relativePath) => File.Exists(GetPath(relativePath));

    public static string ReadAllText(string relativePath) => File.ReadAllText(GetPath(relativePath));

    public static Task<string> ReadAllTextAsync(string relativePath, CancellationToken cancellationToken = default) =>
        File.ReadAllTextAsync(GetPath(relativePath), cancellationToken);

    /// <summary>Writes the file, creating its folder if needed.</summary>
    public static void WriteAllText(string relativePath, string contents) =>
        File.WriteAllText(GetPathForWrite(relativePath), contents);

    /// <inheritdoc cref="WriteAllText"/>
    public static Task WriteAllTextAsync(string relativePath, string contents,
                                         CancellationToken cancellationToken = default) =>
        File.WriteAllTextAsync(GetPathForWrite(relativePath), contents, cancellationToken);

    /// <summary>Appends to the file, creating it and its folder if needed.</summary>
    public static void AppendAllText(string relativePath, string contents) =>
        File.AppendAllText(GetPathForWrite(relativePath), contents);

    /// <inheritdoc cref="AppendAllText"/>
    public static Task AppendAllTextAsync(string relativePath, string contents,
                                          CancellationToken cancellationToken = default) =>
        File.AppendAllTextAsync(GetPathForWrite(relativePath), contents, cancellationToken);

    public static FileStream OpenRead(string relativePath) => File.OpenRead(GetPath(relativePath));

    /// <summary>Opens the file for writing (replacing it), creating its folder if needed.</summary>
    public static FileStream OpenWrite(string relativePath) =>
        new(GetPathForWrite(relativePath), FileMode.Create, FileAccess.Write, FileShare.None);

    /// <summary>
    ///     Like <see cref="GetPath"/>, creating the file's folder if it doesn't exist. For code that opens the file
    ///     itself, such as a database connection string.
    /// </summary>
    public static string GetPathForWrite(string relativePath)
    {
        var path = GetPath(relativePath);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        return path;
    }
}
