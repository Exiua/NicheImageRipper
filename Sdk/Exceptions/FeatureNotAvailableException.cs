using System.Runtime.CompilerServices;

namespace NicheImageRipper.Sdk.Exceptions;

public class FeatureNotAvailableException : RipperException
{
    public FeatureNotAvailableException(string feature, [CallerMemberName] string methodName = "")
        : base($"Method '{methodName}' cannot execute because required feature '{feature}' is not available.")
    {
    }

    public FeatureNotAvailableException(string feature, Exception inner, [CallerMemberName] string methodName = "")
        : base($"Method '{methodName}' cannot execute because required feature '{feature}' is not available.", inner)
    {
    }
}