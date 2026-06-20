namespace NicheImageRipper.Common.Exceptions;

public class AttributeNotFoundException : CommonException
{
    public AttributeNotFoundException()
    {
    }

    public AttributeNotFoundException(string message) : base(message)
    {
    }

    public AttributeNotFoundException(string message, Exception inner) : base(message, inner)
    {
    }
}