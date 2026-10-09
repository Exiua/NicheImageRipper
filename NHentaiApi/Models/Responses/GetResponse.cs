namespace NHentaiApi.Models.Responses;

public class GetResponse<T> where T : class
{
    public GetResult Result { get; init; }

    public T? Gallery { get; init; }

    public string? Message { get; init; }

    public Exception? Exception { get; init; }

    public bool IsSuccess => Result == GetResult.Success;
    public bool IsRateLimited => Result == GetResult.RateLimited;
    public bool IsError => Result == GetResult.Error;

    public static GetResponse<T> Success(T gallery) =>
        new()
        {
            Result = GetResult.Success,
            Gallery = gallery
        };

    public static GetResponse<T> RateLimited(string message) =>
        new()
        {
            Result = GetResult.RateLimited,
            Message = message
        };

    public static GetResponse<T> Error(
        string message,
        Exception? exception = null) =>
        new()
        {
            Result = GetResult.Error,
            Message = message,
            Exception = exception
        };
}