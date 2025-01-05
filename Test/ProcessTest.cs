using System.Diagnostics;

namespace Test;

public static class ProcessTest
{
    public static void RunFfmpeg(string[] cmd)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-loglevel quiet {string.Join(" ", cmd)}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        // process.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
        // process.ErrorDataReceived += (sender, args) => Console.Error.WriteLine(args.Data);
        
        process.Start();
        // process.BeginOutputReadLine();
        // process.BeginErrorReadLine();
        process.WaitForExit();
        var exitCode = process.ExitCode;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
    }

    public static bool CheckForFfmpeg(bool simulateNoPath)
    {
        return CheckForProcess("ffmpeg", "-version", simulateNoPath);
    }
    
    public static bool CheckForYtDlp(bool simulateNoPath)
    {
        return CheckForProcess("yt-dlp", "--version", simulateNoPath);
    }
    
    public static bool CheckForMegaCli(bool simulateNoPath)
    {
        return CheckForProcess("mega-version", "-v", simulateNoPath);
    }

    private static bool CheckForProcess(string filename, string arguments, bool simulateNoPath)
    {
        if (simulateNoPath)
        {
            Environment.SetEnvironmentVariable("PATH", "");
        }
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = filename,
                Arguments = arguments,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        try
        {
            process.Start();
            process.WaitForExit();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
        
        return true;
    }
}