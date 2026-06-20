namespace NicheImageRipper.Core.Exceptions;

public class MissingCookieException : RipperException
{
    public MissingCookieException(string cookieName) : base($"Missing cookie: {cookieName}. Please provide the necessary cookie in the config file to parse this site.")
    {
        
    }
}