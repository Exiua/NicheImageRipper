using System.Diagnostics;
using System.Text;
using NicheImageRipper.Sdk.Common.ExtensionMethods;
using NicheImageRipper.Sdk.Enums;
using NicheImageRipper.Sdk.Exceptions;
using NicheImageRipper.Sdk.Features;
using Serilog;

namespace NicheImageRipper.Sdk.Utility;

public static class ProcessRunner
{
    private static readonly ILogger Logger = Log.ForContext(typeof(ProcessRunner));
    
    public static async Task<(int ExitCode, string? Output, string? Error)> RunSubprocess(
        string executable, string[]? arguments = null, bool captureOutput = false, bool captureError = false,
        string? startMessage = null, string? endMessage = null, CancellationToken cancellationToken = default)
    {
        if (startMessage is not null)
        {
            Logger.Information("{StartMessage:l}", startMessage);
        }

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments is null ? "" : " ".Join(arguments),
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureError,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        StringBuilder? output = null;
        if (captureOutput)
        {
            output = new StringBuilder();
            process.OutputDataReceived += (_, args) =>
            {
                var data = args.Data ?? "null";
                output.AppendLine(data);
                Logger.Debug("{Data:l}", data);
            };
        }

        StringBuilder? error = null;
        if (captureError)
        {
            error = new StringBuilder();
            process.ErrorDataReceived += (_, args) =>
            {
                var data = args.Data ?? "null";
                error.AppendLine(data);
                Logger.Debug("{Data:l}", data);
            };
        }

        process.Start();
        if (captureOutput)
        {
            process.BeginOutputReadLine();
        }

        if (captureError)
        {
            process.BeginErrorReadLine();
        }

        await process.WaitForExitAsync(cancellationToken);
        var exitCode = process.ExitCode;
        if (endMessage is not null)
        {
            Logger.Information("{EndMessage:l}", endMessage);
        }

        return (exitCode, output?.ToString(), error?.ToString());
    }
    
    /// <summary>
    /// Probes whether <paramref name="executable"/> can be launched at all, by starting it with
    /// <paramref name="arguments"/> and waiting for it to exit. Used as the detector for
    /// subprocess-backed <see cref="IAvailableFeatures"/> keys (ffmpeg, yt-dlp, mega-cmd, steamcmd).
    /// </summary>
    public static bool CheckForProcess(string filename, string arguments)
    {
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

    public static async Task<FfmpegStatusCode> RunFfmpeg(string[] cmd,
        string startMessage = "Starting ffmpeg download", string endMessage = "Ffmpeg download finished",
        bool displayOutput = false, CancellationToken cancellationToken = default)
    {
        var available = AvailableFeatureManager.AvailableFeatures.HasFeature(
            FeatureKeys.Ffmpeg, () => CheckForProcess("ffmpeg", "-version"));
        if (!available)
        {
            throw new FeatureNotAvailableException(FeatureKeys.Ffmpeg);
        }

        cmd = !displayOutput ? ["-loglevel", "quiet", "-y", ..cmd] : ["-y", ..cmd];

        Logger.Debug("ffmpeg {cmd}", string.Join(" ", cmd));
        var (exitCode, _, _) = await RunSubprocess("ffmpeg", cmd, captureError: displayOutput,
            startMessage: startMessage, endMessage: endMessage, cancellationToken: cancellationToken);
        if (exitCode != 0)
        {
            Logger.Error("Failed to run ffmpeg: {ExitCode}", exitCode);
        }

        return (FfmpegStatusCode)exitCode;
    }
}