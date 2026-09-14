namespace WiiSharp;

/// <summary>
/// The fields that follow the ticket and locate the TMD, certificates and data.
/// </summary>
public sealed class PartitionHeader
{
    /// <summary>
    /// Bytes <see cref="Parse"/> needs.
    /// </summary>
    public const int Size = 0x1C;

    private PartitionHeader(byte[] bytes)
    {
        TmdSize = BigEndian.ReadUInt32(bytes, 0x00);
        TmdOffset = (long)BigEndian.ReadUInt32(bytes, 0x04) << 2;
        CertificateChainSize = BigEndian.ReadUInt32(bytes, 0x08);
        CertificateChainOffset = (long)BigEndian.ReadUInt32(bytes, 0x0C) << 2;
        H3Offset = (long)BigEndian.ReadUInt32(bytes, 0x10) << 2;
        DataOffset = (long)BigEndian.ReadUInt32(bytes, 0x14) << 2;
        DataSize = (long)BigEndian.ReadUInt32(bytes, 0x18) << 2;
    }

    /// <summary>
    /// Certificate chain offset within the partition.
    /// </summary>
    public long CertificateChainOffset { get; }
    /// <summary>
    /// Certificate chain length.
    /// </summary>
    public uint CertificateChainSize { get; }
    /// <summary>
    /// Encrypted data offset within the partition.
    /// </summary>
    public long DataOffset { get; }
    /// <summary>
    /// Encrypted data length.
    /// </summary>
    public long DataSize { get; }
    /// <summary>
    /// H3 hash table offset within the partition.
    /// </summary>
    public long H3Offset { get; }
    /// <summary>
    /// TMD offset within the partition.
    /// </summary>
    public long TmdOffset { get; }
    /// <summary>
    /// TMD length.
    /// </summary>
    public uint TmdSize { get; }

    /// <summary>
    /// Parses the bytes right after the ticket.
    /// </summary>
    /// <param name="bytes">At least <see cref="Size"/> bytes.</param>
    public static PartitionHeader Parse(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length < Size)
            throw new ArgumentException($"Need at least {Size} bytes.", nameof(bytes));

        return new PartitionHeader(bytes);
    }
}
