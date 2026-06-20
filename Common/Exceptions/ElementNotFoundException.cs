namespace NicheImageRipper.Common.Exceptions;

public class ElementNotFoundException : CommonException
{
    public ElementNotFoundException()
    {
    }

    public ElementNotFoundException(string message) : base(message)
    {
    }

    public ElementNotFoundException(string message, Exception inner) : base(message, inner)
    {
    }
}