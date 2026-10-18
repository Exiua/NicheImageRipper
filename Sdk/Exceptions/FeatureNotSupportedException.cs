namespace NicheImageRipper.Sdk.Exceptions;

/// <summary>A page type or feature a parser doesn't handle (named to avoid clashing with System.NotSupportedException).</summary>
public class FeatureNotSupportedException : RipperException
{
    public string FeatureName { get; }
    public string Reason { get; }
    
    public FeatureNotSupportedException(string featureName, string reason)
        : base($"The feature '{featureName}' is not supported. Reason: {reason}")
    {
        FeatureName = featureName;
        Reason = reason;
    }

    public FeatureNotSupportedException(string featureName, string reason, Exception innerException) 
        : base($"The feature '{featureName}' is not supported. Reason: {reason}", innerException)
    {
        FeatureName = featureName;
        Reason = reason;
    }
}