namespace WiiSharp;

/// <summary>
/// Seekable read-only view of a WBFS disc as a plain image.
/// </summary>
internal sealed class WbfsDiscStream : Stream
{
    private readonly WbfsDisc _disc;
    private readonly WbfsFile _file;
    private readonly long _length;
    private long _position;

    public WbfsDiscStream(WbfsFile file, WbfsDisc disc)
    {
        _file = file;
        _disc = disc;
        _length = disc.StoredLength;
    }

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set => _position = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (buffer is null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));
        _file.ThrowIfDisposed();

        var total = 0;
        while (count > 0 && _position < _length)
        {
            var read = _disc.ReadAt(_position, buffer, offset, (int)Math.Min(count, _length - _position));
            if (read == 0)
                break;
            _position += read;
            offset += read;
            count -= read;
            total += read;
        }
        return total;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        var target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        Position = target;
        return _position;
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
