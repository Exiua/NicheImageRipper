namespace NicheImageRipper.Common.Exceptions;

public class NotEnoughDiskSpaceException(Exception inner) : CommonException("Not enough disk space", inner);
