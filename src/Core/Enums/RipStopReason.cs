namespace NicheImageRipper.Core.Enums;

/// <summary>Why <see cref="NicheImageRipper.Rip"/> returned.</summary>
public enum RipStopReason
{
    /// <summary>Every queued URL was processed.</summary>
    QueueFinished,

    /// <summary>
    ///     Stopped because the disk is full: every later download would fail the same way. The remaining URLs,
    ///     including the one being ripped, stay queued.
    /// </summary>
    OutOfDiskSpace,
}
