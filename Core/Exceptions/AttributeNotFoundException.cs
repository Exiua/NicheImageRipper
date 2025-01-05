namespace Core.Exceptions;

public class AttributeNotFoundException : RipperException
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