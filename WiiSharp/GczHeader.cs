namespace WiiSharp;

/// <summary>
/// The 32-byte little-endian header of a Dolphin GCZ image.
/// </summary>
public sealed class GczHeader
{
    /// <summary>
    /// Header length.
    /// </summary>
    public const int Size = 0x20;
    /// <summary>
    /// Magic at offset 0.
    /// </summary>
    public const uint Magic = 0xB10BC001;

    private GczHeader(uint subType, ulong compressedDataSize, ulong dataSize, uint blockSize, uint blockCount)
    {
        SubType = subType;
        CompressedDataSize = compressedDataSize;
        DataSize = dataSize;
        BlockSize = blockSize;
        BlockCount = blockCount;
    }

    /// <summary>
    /// Number of blocks.
    /// </summary>
    public uint BlockCount { get; }
    /// <summary>
    /// Decompressed bytes per block.
    /// </summary>
    public uint BlockSize { get; }
    /// <summary>
    /// Bytes of block data after the tables.
    /// </summary>
    public ulong CompressedDataSize { get; }
    /// <summary>
    /// Decompressed image length.
    /// </summary>
    public ulong DataSize { get; }
    /// <summary>
    /// 0 for GameCube, 1 for Wii.
    /// </summary>
    public uint SubType { get; }

    /// <summary>
    /// Parses the header.
    /// </summary>
    /// <param name="bytes">At least <see cref="Size"/> bytes.</param>
    /// <exception cref="InvalidDataException">Wrong magic or inconsistent sizes.</exception>
    public static GczHeader Parse(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length < Size)
            throw new ArgumentException($"Need at least {Size} bytes.", nameof(bytes));
        if (BitConverter.ToUInt32(bytes, 0) != Magic)
            throw new InvalidDataException("Not a GCZ image.");

        var header = new GczHeader(
            BitConverter.ToUInt32(bytes, 4),
            BitConverter.ToUInt64(bytes, 8),
            BitConverter.ToUInt64(bytes, 16),
            BitConverter.ToUInt32(bytes, 24),
            BitConverter.ToUInt32(bytes, 28));
        if (header.BlockSize == 0 || header.BlockCount == 0)
            throw new InvalidDataException("GCZ image has no blocks.");
        if (header.DataSize == 0 || (header.DataSize + header.BlockSize - 1) / header.BlockSize != header.BlockCount)
            throw new InvalidDataException("GCZ block count does not cover the data size.");
        return header;
    }
}
