using System.Text;

namespace WiiSharp;

/// <summary>
/// The block NKit writes at 0x200 of a compacted disc header: source checksum and length, plus the checksum patch that makes the compact file's CRC match the source.
/// </summary>
public sealed class NkitHeader
{
    /// <summary>
    /// Where the block sits in the disc header.
    /// </summary>
    public const int Offset = 0x200;
    /// <summary>
    /// Bytes the block spans.
    /// </summary>
    public const int Size = 0x20;
    /// <summary>
    /// Magic at <see cref="Offset"/>.
    /// </summary>
    public const string Magic = "NKIT";
    /// <summary>
    /// Format version that follows the magic.
    /// </summary>
    public const string Version = " v01";
    /// <summary>
    /// Where the checksum patch sits.
    /// </summary>
    public const int PatchOffset = Offset + 0x0C;

    /// <summary>
    /// Creates a new instance of the <see cref="NkitHeader"/> class.
    /// </summary>
    /// <param name="isWii">Wii images store their length in 4-byte units.</param>
    /// <param name="sourceCrc">CRC-32 of the full image.</param>
    /// <param name="sourceLength">Length of the full image in bytes.</param>
    /// <param name="forcedJunkId">Disc ID the junk was seeded with when it differs from the header's, else zero.</param>
    /// <param name="updatePartitionCrc">CRC-32 of a removed update partition, else zero.</param>
    public NkitHeader(bool isWii, uint sourceCrc, long sourceLength, uint forcedJunkId = 0, uint updatePartitionCrc = 0)
    {
        if (sourceLength < 0 || (isWii ? sourceLength >> 2 : sourceLength) > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(sourceLength));

        IsWii = isWii;
        SourceCrc = sourceCrc;
        SourceLength = sourceLength;
        ForcedJunkId = forcedJunkId;
        UpdatePartitionCrc = updatePartitionCrc;
    }

    /// <summary>
    /// Disc ID the junk was seeded with when it differs from the header's, else zero.
    /// </summary>
    public uint ForcedJunkId { get; }
    /// <summary>
    /// True for a Wii image.
    /// </summary>
    public bool IsWii { get; }
    /// <summary>
    /// Bytes that make the compact file's CRC-32 equal <see cref="SourceCrc"/>; zero until patched.
    /// </summary>
    public uint Patch { get; private set; }
    /// <summary>
    /// CRC-32 of the full image.
    /// </summary>
    public uint SourceCrc { get; }
    /// <summary>
    /// Length of the full image in bytes.
    /// </summary>
    public long SourceLength { get; }
    /// <summary>
    /// CRC-32 of a removed update partition, else zero.
    /// </summary>
    public uint UpdatePartitionCrc { get; }

    /// <summary>
    /// True when the magic is present.
    /// </summary>
    /// <param name="header">At least the first 0x204 bytes of a disc header.</param>
    public static bool IsPresent(byte[] header)
    {
        if (header is null)
            throw new ArgumentNullException(nameof(header));

        return header.Length >= Offset + 4 && Encoding.ASCII.GetString(header, Offset, 4) == Magic;
    }

    /// <summary>
    /// Reads the block.
    /// </summary>
    /// <param name="header">At least the first 0x220 bytes of a disc header.</param>
    /// <exception cref="InvalidDataException">No magic, or a version other than <see cref="Version"/>.</exception>
    public static NkitHeader Parse(byte[] header)
    {
        if (header is null)
            throw new ArgumentNullException(nameof(header));
        if (header.Length < Offset + Size)
            throw new ArgumentException($"Need at least {Offset + Size} bytes.", nameof(header));
        if (!IsPresent(header))
            throw new InvalidDataException("Not an NKit image.");
        if (Encoding.ASCII.GetString(header, Offset + 4, 4) != Version)
            throw new InvalidDataException("Unsupported NKit version.");

        var isWii = BigEndian.ReadUInt32(header, 0x18) == DiscFormat.WiiMagic;
        var length = (long)BigEndian.ReadUInt32(header, Offset + 0x10);
        return new NkitHeader(isWii, BigEndian.ReadUInt32(header, Offset + 0x08), isWii ? length << 2 : length, BigEndian.ReadUInt32(header, Offset + 0x14), BigEndian.ReadUInt32(header, Offset + 0x18))
        {
            Patch = BigEndian.ReadUInt32(header, PatchOffset),
        };
    }

    /// <summary>
    /// Writes the block into a disc header.
    /// </summary>
    /// <param name="header">At least the first 0x220 bytes of a disc header.</param>
    public void Write(byte[] header)
    {
        if (header is null)
            throw new ArgumentNullException(nameof(header));
        if (header.Length < Offset + Size)
            throw new ArgumentException($"Need at least {Offset + Size} bytes.", nameof(header));

        Array.Clear(header, Offset, Size);
        Encoding.ASCII.GetBytes(Magic).CopyTo(header, Offset);
        Encoding.ASCII.GetBytes(Version).CopyTo(header, Offset + 4);
        BigEndian.WriteUInt32(header, Offset + 0x08, SourceCrc);
        BigEndian.WriteUInt32(header, PatchOffset, Patch);
        BigEndian.WriteUInt32(header, Offset + 0x10, (uint)(IsWii ? SourceLength >> 2 : SourceLength));
        BigEndian.WriteUInt32(header, Offset + 0x14, ForcedJunkId);
        BigEndian.WriteUInt32(header, Offset + 0x18, UpdatePartitionCrc);
    }
}
