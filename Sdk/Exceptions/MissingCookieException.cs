namespace Sdk.Exceptions;

public class MissingCookieException : RipperException
{
    public MissingCookieException(string cookieName, string parserName) : base(
        $"Parser: {parserName} missing cookie: {cookieName}. Please provide the necessary cookie in the config file to parse this site.")
    {
    }
}