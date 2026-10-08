namespace PatreonApiClient.Models.Exceptions;

public class PatreonClientException : Exception
{
    public PatreonClientException(string message) : base(message)
    {
    }
}