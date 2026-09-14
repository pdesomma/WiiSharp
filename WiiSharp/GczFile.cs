namespace WiiSharp;

/// <summary>
/// A Dolphin GCZ image: header, block pointer table, Adler-32 table, then zlib or stored blocks.
/// </summary>
public sealed class GczFile : IDisposable
{
    /// <summary>
    /// Pointer bit marking a block stored without compression.
    /// </summary>
    public const ulong StoredFlag = 1UL << 63;

    private readonly Stream _stream;
    private readonly bool _ownsStream;

    private GczFile(Stream stream, bool ownsStream, GczHeader header, ulong[] pointers, uint[] hashes)
    {
        _stream = stream;
        _ownsStream = ownsStream;
        Header = header;
        Pointers = pointers;
        Hashes = hashes;
        DataOffset = GczHeader.Size + (long)header.BlockCount * 12;
    }

    /// <summary>
    /// Where block data begins.
    /// </summary>
    public long DataOffset { get; }
    /// <summary>
    /// Adler-32 of each stored block.
    /// </summary>
    public IReadOnlyList<uint> Hashes { get; }
    /// <summary>
    /// Header.
    /// </summary>
    public GczHeader Header { get; }
    /// <summary>
    /// Raw pointer of each block, <see cref="StoredFlag"/> included.
    /// </summary>
    public IReadOnlyList<ulong> Pointers { get; }

    /// <summary>
    /// Opens an image file.
    /// </summary>
    /// <param name="path">.gcz path.</param>
    public static GczFile Open(string path)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        var stream = File.OpenRead(path);
        try
        {
            return Read(stream, ownsStream: true);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Reads the tables of an image.
    /// </summary>
    /// <param name="stream">Seekable image.</param>
    /// <param name="ownsStream">Dispose the stream with the file.</param>
    public static GczFile Read(Stream stream, bool ownsStream = false)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));
        if (!stream.CanSeek)
            throw new ArgumentException("Image must be seekable.", nameof(stream));

        var header = GczHeader.Parse(stream.ReadExactlyAt(0, GczHeader.Size));
        var tables = stream.ReadExactly(checked((int)(header.BlockCount * 12)));
        var pointers = new ulong[header.BlockCount];
        var hashes = new uint[header.BlockCount];
        for (var i = 0; i < pointers.Length; i++)
            pointers[i] = BitConverter.ToUInt64(tables, i * 8);
        for (var i = 0; i < hashes.Length; i++)
            hashes[i] = BitConverter.ToUInt32(tables, pointers.Length * 8 + i * 4);
        return new GczFile(stream, ownsStream, header, pointers, hashes);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownsStream)
            _stream.Dispose();
    }

    /// <summary>
    /// Seekable view of the decompressed image.
    /// </summary>
    public Stream OpenStream() => new GczStream(this);

    internal byte[] ReadStoredBlock(int index, out bool stored)
    {
        var pointer = Pointers[index];
        stored = (pointer & StoredFlag) != 0;
        var start = pointer & ~StoredFlag;
        var end = index + 1 < Pointers.Count ? Pointers[index + 1] & ~StoredFlag : Header.CompressedDataSize;
        if (end < start)
            throw new InvalidDataException($"GCZ block {index} has a negative length.");

        var bytes = _stream.ReadExactlyAt(DataOffset + (long)start, checked((int)(end - start)));
        if (Adler32.Compute(bytes, 0, bytes.Length) != Hashes[index])
            throw new InvalidDataException($"GCZ block {index} is corrupt.");
        return bytes;
    }
}
