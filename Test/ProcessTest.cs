using System.Diagnostics;

namespace Test;

public class ProcessTest
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

    public static void CheckForFfmpeg()
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        
        process.StartInfo.Environment.Clear(); // Simulating user not having ffmpeg
        process.Start();
        process.WaitForExit();

        Console.WriteLine(process.ExitCode);
    }
}