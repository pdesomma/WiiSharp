using System.IO.Compression;

namespace WiiSharp;

/// <summary>
/// Read-only view of a GCZ image, decoding one block at a time.
/// </summary>
public sealed class GczStream : Stream
{
    private readonly byte[] _block;
    private readonly GczFile _file;
    private readonly long _length;
    private int _cached = -1;
    private long _position;

    internal GczStream(GczFile file)
    {
        _file = file;
        _length = (long)file.Header.DataSize;
        _block = new byte[file.Header.BlockSize];
    }

    /// <inheritdoc/>
    public override bool CanRead => true;
    /// <inheritdoc/>
    public override bool CanSeek => true;
    /// <inheritdoc/>
    public override bool CanWrite => false;
    /// <inheritdoc/>
    public override long Length => _length;
    /// <inheritdoc/>
    public override long Position
    {
        get => _position;
        set => _position = value < 0 ? throw new ArgumentOutOfRangeException(nameof(value)) : value;
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (buffer is null)
            throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        var total = 0;
        var blockSize = _file.Header.BlockSize;
        while (count > 0 && _position < _length)
        {
            var index = (int)(_position / blockSize);
            var within = (int)(_position % blockSize);
            Load(index);
            var chunk = (int)Math.Min(count, Math.Min(blockSize - within, _length - _position));
            Array.Copy(_block, within, buffer, offset, chunk);
            _position += chunk;
            offset += chunk;
            count -= chunk;
            total += chunk;
        }
        return total;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        Position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        return _position;
    }

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    private void Load(int index)
    {
        if (index == _cached)
            return;

        var raw = _file.ReadStoredBlock(index, out var stored);
        Array.Clear(_block, 0, _block.Length);
        if (stored)
        {
            if (raw.Length > _block.Length)
                throw new InvalidDataException($"GCZ block {index} is larger than the block size.");
            raw.CopyTo(_block, 0);
        }
        else
        {
            if (raw.Length < 2)
                throw new InvalidDataException($"GCZ block {index} is not a zlib stream.");
            using var deflate = new DeflateStream(new MemoryStream(raw, 2, raw.Length - 2), CompressionMode.Decompress);
            var read = 0;
            int n;
            while (read < _block.Length && (n = deflate.Read(_block, read, _block.Length - read)) > 0)
                read += n;
        }
        _cached = index;
    }
}
