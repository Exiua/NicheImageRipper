namespace PixivApi.Exceptions;

public class PixivRequestException : PixivApiException
{
    public Dictionary<string, string> Headers { get; set; }
    public string Body { get; set; }

    public PixivRequestException(string message, Dictionary<string, string> headers, string body) : base(message)
    {
        Headers = headers;
        Body = body;
    }
    
    public PixivRequestException(string message, Exception inner, Dictionary<string, string> headers, string body) : base(message, inner)
    {
        Headers = headers;
        Body = body;
    }
}