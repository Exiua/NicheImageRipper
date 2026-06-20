namespace NicheImageRipper.Core.Exceptions;

public class ApiKeyRequired : RipperException
{
    public ApiKeyRequired()
    {
    }

    public ApiKeyRequired(string site) : base($"API key is required for {site}. Please check your configuration.")
    {
    }

    public ApiKeyRequired(string site, Exception inner) : base($"API key is required for {site}. Please check your configuration.", inner)
    {
    }
}