namespace IwaraApiClient.Models;

public struct Result<TOk, TError> where TOk : notnull where TError : notnull
{
    private readonly TOk? _ok;
    private readonly TError? _error;

    public bool IsOk { get; }

    public bool IsError => !IsOk;

    private Result(TOk ok)
    {
        _ok = ok;
        _error = default;
        IsOk = true;
    }
    
    private Result(TError error)
    {
        _error = error;
        _ok = default;
        IsOk = false;
    }
    
    public TOk Unwrap()
    {
        return IsOk ? _ok! : throw new InvalidOperationException("Cannot unwrap a Result that is an error.");
    }
    
    public TError UnwrapError()
    {
        return IsError ? _error! : throw new InvalidOperationException("Cannot unwrap a Result that is ok.");
    }
    
    public static Result<TOk, TError> Ok(TOk ok) => new(ok);
    public static Result<TOk, TError> Error(TError error) => new(error);
    
    public static implicit operator Result<TOk, TError>(TOk ok) => Ok(ok);
    public static implicit operator Result<TOk, TError>(TError error) => Error(error);
}