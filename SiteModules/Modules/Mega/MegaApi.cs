using System.Diagnostics;

namespace NicheImageRipper.SiteModules.Modules.Mega;

public static class MegaApi
{
    public static event Action<string?>? OnOutputReceived;
    public static event Action<string?>? OnErrorReceived;
    
    public static bool Login(string email, string password)
    {
        string[] cmd = ["mega-login", email, $"\"{password}\""];
        
        using var process = RunSubprocess(cmd);

        var stderr = process.StandardError.ReadToEnd();
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
        string[] cmd = ["mega-get", url, $"\"{dest}\""];
        
        using var process = RunSubprocess(cmd);
        return process.ExitCode == 0;
    }
    
    public static async Task<bool> DownloadAsync(string url, string dest, CancellationToken cancellationToken)
    {
        string[] cmd = ["mega-get", url, $"\"{dest}\""];
        
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

        var stdout = process.StandardOutput.ReadToEnd();
        return stdout.Split(' ')[^1].Trim();
    }

    private static Process RunSubprocess(IEnumerable<string> cmd)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C {string.Join(" ", cmd)}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        process.Start();
        process.WaitForExit();

        return process;
    }
    
    private static async Task<Process> RunSubprocessAsync(IEnumerable<string> cmd, CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/C {string.Join(" ", cmd)}",
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