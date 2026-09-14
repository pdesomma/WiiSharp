namespace WiiSharp;

/// <summary>
/// Read-only view of a plaintext partition's payload with the hash blocks skipped.
/// </summary>
public sealed class PartitionDataStream : Stream
{
    private readonly Stream _disc;
    private readonly long _dataStart;
    private readonly long _length;
    private long _position;

    /// <summary>
    /// Creates a new instance of the <see cref="PartitionDataStream"/> class.
    /// </summary>
    /// <param name="disc">Seekable disc image whose partition clusters are plaintext; not disposed.</param>
    /// <param name="partition">Partition to expose.</param>
    public PartitionDataStream(Stream disc, Partition partition)
    {
        if (disc is null)
            throw new ArgumentNullException(nameof(disc));
        if (partition is null)
            throw new ArgumentNullException(nameof(partition));
        if (!disc.CanSeek)
            throw new ArgumentException("Disc image must be seekable.", nameof(disc));

        _disc = disc;
        _dataStart = partition.DataStart;
        _length = partition.Header.DataSize / DiscFormat.ClusterSize * DiscFormat.ClusterDataSize;
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
        while (count > 0 && _position < _length)
        {
            var cluster = _position / DiscFormat.ClusterDataSize;
            var within = (int)(_position % DiscFormat.ClusterDataSize);
            var chunk = (int)Math.Min(count, Math.Min(DiscFormat.ClusterDataSize - within, _length - _position));
            _disc.Position = _dataStart + cluster * DiscFormat.ClusterSize + DiscFormat.ClusterHashSize + within;
            var read = _disc.Read(buffer, offset, chunk);
            if (read == 0)
                throw new EndOfStreamException("Disc image ends inside the partition.");
            _position += read;
            offset += read;
            count -= read;
            total += read;
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
}
