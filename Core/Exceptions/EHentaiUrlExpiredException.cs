namespace NicheImageRipper.Core.Exceptions;

public class EHentaiUrlExpiredException : RipperException
{
    public int ResumeIndex { get; set; }
}