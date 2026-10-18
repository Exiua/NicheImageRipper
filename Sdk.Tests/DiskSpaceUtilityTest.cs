using NicheImageRipper.Sdk.Utility;

namespace NicheImageRipper.Sdk.Tests;

public class DiskSpaceUtilityTest
{
    // What the runtime reports for a full disk on the platform running the tests
    private static readonly int DiskFullHResult = OperatingSystem.IsWindows() ? unchecked((int)0x80070070) : 28;

    [Fact]
    public void DiskFullIOExceptionIsDetected()
    {
        Assert.True(DiskSpaceUtility.IsDiskFull(new IOException("There is not enough space on the disk.", DiskFullHResult)));
    }

    [Fact]
    public void WrappedDiskFullIsDetected()
    {
        var diskFull = new IOException("disk full", DiskFullHResult);

        Assert.True(DiskSpaceUtility.IsDiskFull(new InvalidOperationException("outer", diskFull)));
        Assert.True(DiskSpaceUtility.IsDiskFull(new AggregateException(new TimeoutException(), diskFull)));
    }

    [Fact]
    public void OtherErrorsAreNotDiskFull()
    {
        Assert.False(DiskSpaceUtility.IsDiskFull(null));
        Assert.False(DiskSpaceUtility.IsDiskFull(new IOException("The response ended prematurely.")));
        Assert.False(DiskSpaceUtility.IsDiskFull(new UnauthorizedAccessException()));
    }
}
