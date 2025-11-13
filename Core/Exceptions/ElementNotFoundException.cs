namespace Core.Exceptions;

public class ElementNotFoundException : RipperException
{
    public ElementNotFoundException()
    {
    }

    public ElementNotFoundException(string xpath) : base($"Element not found for XPath: {xpath}")
    {
    }

    public ElementNotFoundException(string xpath, Exception inner) : base($"Element not found for XPath: {xpath}", inner)
    {
    } 
}