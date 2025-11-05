namespace PixivApi.Exceptions;

public class PixivApiException : Exception
{
    public PixivApiException() : base()
    {
        
    }
    
    public PixivApiException(string message) : base(message)
    {
        
    }

    public PixivApiException(string message, Exception innerException) : base(message, innerException)
    {
        
    }
}