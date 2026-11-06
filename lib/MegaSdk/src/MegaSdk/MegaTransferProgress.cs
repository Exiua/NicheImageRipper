namespace MegaSdk;

/// <param name="FileName">Name of the file or folder being transferred.</param>
/// <param name="TransferredBytes">Bytes transferred so far.</param>
/// <param name="TotalBytes">Total bytes of the transfer (for folders, of all files in it).</param>
/// <param name="Speed">Current speed in bytes per second.</param>
public readonly record struct MegaTransferProgress(
    string? FileName,
    long TransferredBytes,
    long TotalBytes,
    long Speed,
    MegaTransferState State);
