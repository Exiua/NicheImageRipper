namespace ImpersonateClient;

internal sealed class CurlResponseCapture
{
    public MemoryStream Body { get; } = new();
    public MemoryStream Headers { get; } = new();

    public unsafe nuint WriteBody(byte* buffer, nuint size, nuint nitems, nint userdata)
    {
        return WriteToStream(Body, buffer, size, nitems);
    }

    public unsafe nuint WriteHeader(byte* buffer, nuint size, nuint nitems, nint userdata)
    {
        return WriteToStream(Headers, buffer, size, nitems);
    }

    private static unsafe nuint WriteToStream(
        Stream stream,
        byte* buffer,
        nuint size,
        nuint nitems)
    {
        nuint byteCount = size * nitems;

        if (byteCount == 0)
        {
            return 0;
        }

        ReadOnlySpan<byte> data = new(buffer, checked((int)byteCount));
        stream.Write(data);
        return byteCount;
    }
}