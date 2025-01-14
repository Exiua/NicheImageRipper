using System.Diagnostics;

namespace Core.FileDownloading;

public static class MegaApi
{
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
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        process.Start();
        process.WaitForExit();

        return process;
    }
}