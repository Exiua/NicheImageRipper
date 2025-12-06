namespace Core.Exceptions;

public class PornhubUrlExpiredException : RipperException
{
    public int ResumeIndex { get; set; }
}