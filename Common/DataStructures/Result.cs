namespace NicheImageRipper.Common.DataStructures;

public struct Result<TOk, TError> where TOk : notnull where TError : notnull
{
    private readonly TOk? _ok;
    private readonly TError? _error;
    
    public bool IsOk => _ok is not null;
    public bool IsError => _error is not null;

    private Result(TOk ok)
    {
        _ok = ok;
        _error = default;
    }
    
    private Result(TError error)
    {
        _error = error;
        _ok = default;
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