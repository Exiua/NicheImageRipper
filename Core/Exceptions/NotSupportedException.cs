namespace Core.Exceptions;

public class NotSupportedException : RipperException
{
    public string FeatureName { get; }
    public string Reason { get; }
    
    public NotSupportedException(string featureName, string reason)
        : base($"The feature '{featureName}' is not supported. Reason: {reason}")
    {
        FeatureName = featureName;
        Reason = reason;
    }

    public NotSupportedException(string featureName, string reason, Exception innerException) 
        : base($"The feature '{featureName}' is not supported. Reason: {reason}", innerException)
    {
        FeatureName = featureName;
        Reason = reason;
    }
}