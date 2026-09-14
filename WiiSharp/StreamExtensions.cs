namespace WiiSharp;

internal static class StreamExtensions
{
    public static void CopyExactly(this Stream source, Stream destination, long count, byte[] buffer, CancellationToken cancellationToken)
    {
        while (count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, count));
            if (read == 0)
                throw new EndOfStreamException();
            destination.Write(buffer, 0, read);
            count -= read;
        }
    }

    public static byte[] ReadExactly(this Stream stream, int count)
    {
        var bytes = new byte[count];
        stream.ReadExactly(bytes, 0, count);
        return bytes;
    }

    public static void ReadExactly(this Stream stream, byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            var read = stream.Read(buffer, offset, count);
            if (read == 0)
                throw new EndOfStreamException();
            offset += read;
            count -= read;
        }
    }

    public static byte[] ReadExactlyAt(this Stream stream, long position, int count)
    {
        stream.Position = position;
        return stream.ReadExactly(count);
    }
}
