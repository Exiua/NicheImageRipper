using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MegaSdk;

/// <param name="Time">Timestamp formatted by the SDK.</param>
/// <param name="Source">Source location in the SDK, if any.</param>
public readonly record struct MegaLogMessage(string? Time, MegaLogLevel Level, string? Source, string Message);

/// <summary>
///     Process-wide SDK logging (the SDK's logger is global, not per <see cref="MegaClient" />).
/// </summary>
public static unsafe class MegaLogging
{
    private static readonly Lock Lock = new();
    private static Action<MegaLogMessage>? _handler;

    /// <summary>
    ///     Messages above this level are discarded by the SDK.
    /// </summary>
    public static void SetLogLevel(MegaLogLevel level) => NativeMethods.SetLogLevel((int)level);

    /// <summary>
    ///     Sets the handler for SDK log messages, or removes it if null. It's called on SDK threads and must not block.
    /// </summary>
    public static void SetHandler(Action<MegaLogMessage>? handler)
    {
        lock (Lock)
        {
            _handler = handler;
            NativeMethods.SetLogger(handler is null ? null : &OnLog);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnLog(byte* time, int level, byte* source, byte* message)
    {
        try
        {
            _handler?.Invoke(new MegaLogMessage(
                NativeMethods.ReadString(time),
                (MegaLogLevel)level,
                NativeMethods.ReadString(source),
                NativeMethods.ReadString(message) ?? ""));
        }
        catch
        {
            // Exceptions can't propagate into native code.
        }
    }
}
