using System.Diagnostics;

namespace NicheImageRipper.SiteModules.Modules.Mega;

public static class MegaApi
{
    public static event Action<string?>? OnOutputReceived;
    public static event Action<string?>? OnErrorReceived;
    
    public static bool Login(string email, string password)
    {
        string[] cmd = ["mega-login", email, password];
        
        using var process = RunSubprocess(cmd);

        var stderr = _lastError;
        return string.IsNullOrEmpty(stderr);
    }
    
    public static bool Logout()
    {
        string[] cmd = ["mega-logout"];
        
        using var process = RunSubprocess(cmd);
        return process.ExitCode == 0;
    }
    
    public static bool Download(string url, string dest)
    {
        string[] cmd = ["mega-get", url, dest];
        
        using var process = RunSubprocess(cmd);
        return process.ExitCode == 0;
    }
    
    public static async Task<bool> DownloadAsync(string url, string dest, CancellationToken cancellationToken)
    {
        string[] cmd = ["mega-get", url, dest];
        
        using var process = await RunSubprocessAsync(cmd, cancellationToken);
        
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return process.ExitCode == 0;
    }
    
    public static string WhoAmI()
    {
        string[] cmd = ["mega-whoami"];

        using var process = RunSubprocess(cmd);

        var stdout = _lastOutput ?? "";
        return stdout.Split(' ')[^1].Trim();
    }

    /// <summary>
    ///     MEGAcmd's commands are .bat files, so they run through cmd.exe, which parses the command line by its own
    ///     rules. Each argument is quoted so <c>&amp; | &lt; &gt; ^</c> are taken literally. A double quote can't be
    ///     escaped safely for cmd, so such arguments are refused rather than risk injecting a command.
    ///     (<c>%NAME%</c> is still expanded by cmd if it names an environment variable.)
    /// </summary>
    private static string BuildCmdArguments(IEnumerable<string> cmd)
    {
        var quoted = cmd.Select(argument => argument.Contains('"')
            ? throw new ArgumentException("MEGAcmd arguments can't contain a double quote (\").", nameof(cmd))
            : $"\"{argument}\"");
        return $"/S /C \"{string.Join(" ", quoted)}\"";
    }

    private static Process RunSubprocess(IEnumerable<string> cmd)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = BuildCmdArguments(cmd),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        // Read both streams while it runs: waiting first deadlocks once its output fills the pipe buffer
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        _lastOutput = output.GetAwaiter().GetResult();
        _lastError = error.GetAwaiter().GetResult();

        return process;
    }

    // Output of the last synchronous command on this thread, read while it ran (see RunSubprocess)
    [ThreadStatic] private static string? _lastOutput;
    [ThreadStatic] private static string? _lastError;
    
    private static async Task<Process> RunSubprocessAsync(IEnumerable<string> cmd, CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = BuildCmdArguments(cmd),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        var tcs = new TaskCompletionSource<bool>();

        cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch { /* Ignore if already exited or failed to kill */ }
            tcs.TrySetCanceled();
        });

        process.Exited += (_, _) => tcs.TrySetResult(true);
        process.OutputDataReceived += (_, args) => OnOutputReceived?.Invoke(args.Data);
        process.ErrorDataReceived += (_, args) => OnErrorReceived?.Invoke(args.Data);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await tcs.Task.ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken);

        return process;
    }
}