using System.Runtime.CompilerServices;
using Sdk.Enums;

namespace Sdk.Exceptions;

public class FeatureNotAvailableException : RipperException
{
    public FeatureNotAvailableException(ExternalFeatureSupport feature, [CallerMemberName] string methodName = "")
        : base($"Method '{methodName}' cannot execute because required feature '{feature}' is not available.") 
    {
    }

    public FeatureNotAvailableException(ExternalFeatureSupport feature, Exception inner, [CallerMemberName] string methodName = "")
        : base($"Method '{methodName}' cannot execute because required feature '{feature}' is not available.", inner) 
    {
    }
}