namespace NicheImageRipper.Core.Exceptions;

public class ParameterizedParserNotFound(string site)
    : RipperException($"No delegatable (ParameterizedHtmlParser) parser is registered for site: {site}");