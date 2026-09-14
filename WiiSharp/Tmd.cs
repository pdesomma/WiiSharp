namespace WiiSharp;

/// <summary>
/// A Wii title metadata blob: signed header plus content records.
/// </summary>
public sealed class Tmd
{
    /// <summary>
    /// Bytes per content record.
    /// </summary>
    public const int ContentRecordSize = 0x24;
    /// <summary>
    /// Offset of the first content record.
    /// </summary>
    public const int ContentsOffset = 0x1E4;
    /// <summary>
    /// Content type of a disc partition.
    /// </summary>
    public const ushort DiscContentType = 0x0003;
    /// <summary>
    /// Title type of disc games.
    /// </summary>
    public const uint DiscTitleType = 1;
    /// <summary>
    /// Issuer of disc TMDs.
    /// </summary>
    public const string Issuer = "Root-CA00000001-CP00000004";

    private const int FakeSignOffset = 0x1E2;

    private readonly byte[] _bytes;

    private Tmd(byte[] bytes)
    {
        _bytes = bytes;
    }

    /// <summary>
    /// Content records in TMD order.
    /// </summary>
    public IReadOnlyList<TmdContent> Contents
    {
        get
        {
            var count = BigEndian.ReadUInt16(_bytes, 0x1DE);
            var contents = new TmdContent[count];
            for (var i = 0; i < count; i++)
            {
                var at = ContentsOffset + i * ContentRecordSize;
                var hash = new byte[DiscFormat.HashSize];
                Array.Copy(_bytes, at + 0x10, hash, 0, hash.Length);
                contents[i] = new TmdContent(
                    BigEndian.ReadUInt32(_bytes, at),
                    BigEndian.ReadUInt16(_bytes, at + 4),
                    BigEndian.ReadUInt16(_bytes, at + 6),
                    BigEndian.ReadUInt64(_bytes, at + 8),
                    hash);
            }
            return contents;
        }
    }
    /// <summary>
    /// Group ID, the maker code for retail discs.
    /// </summary>
    public ushort GroupId => BigEndian.ReadUInt16(_bytes, 0x198);
    /// <summary>
    /// IOS the title runs under, e.g. 0x0000000100000023 for IOS35.
    /// </summary>
    public ulong IosVersion => BigEndian.ReadUInt64(_bytes, 0x184);
    /// <summary>
    /// Region code.
    /// </summary>
    public DiscRegion Region => (DiscRegion)BigEndian.ReadUInt16(_bytes, 0x19C);
    /// <summary>
    /// Whole length.
    /// </summary>
    public int Size => _bytes.Length;
    /// <summary>
    /// Eight-byte title ID.
    /// </summary>
    public byte[] TitleId
    {
        get
        {
            var id = new byte[8];
            Array.Copy(_bytes, 0x18C, id, 0, 8);
            return id;
        }
    }
    /// <summary>
    /// Title type.
    /// </summary>
    public uint TitleType => BigEndian.ReadUInt32(_bytes, 0x194);

    /// <summary>
    /// Builds an unsigned, fakesigned single-content disc TMD.
    /// </summary>
    /// <param name="titleId">Eight-byte title ID.</param>
    /// <param name="iosVersion">System version the title needs.</param>
    /// <param name="groupId">Group ID.</param>
    /// <param name="region">Region settings copied into the header.</param>
    /// <param name="content">The partition's content record.</param>
    public static Tmd Build(byte[] titleId, ulong iosVersion, ushort groupId, RegionSettings region, TmdContent content)
    {
        if (titleId is null)
            throw new ArgumentNullException(nameof(titleId));
        if (titleId.Length != 8)
            throw new ArgumentException("A title ID is 8 bytes.", nameof(titleId));
        if (region is null)
            throw new ArgumentNullException(nameof(region));
        if (content is null)
            throw new ArgumentNullException(nameof(content));
        if (content.Hash is null || content.Hash.Length != DiscFormat.HashSize)
            throw new ArgumentException("Content hash must be SHA-1.", nameof(content));

        var bytes = new byte[ContentsOffset + ContentRecordSize];
        BigEndian.WriteUInt32(bytes, 0, Signature.Rsa2048Type);
        Signature.WriteIssuer(bytes, Issuer);
        BigEndian.WriteUInt64(bytes, 0x184, iosVersion);
        titleId.CopyTo(bytes, 0x18C);
        BigEndian.WriteUInt32(bytes, 0x194, DiscTitleType);
        BigEndian.WriteUInt16(bytes, 0x198, groupId);
        BigEndian.WriteUInt16(bytes, 0x19C, (ushort)region.Region);
        region.Ratings.CopyTo(bytes, 0x19E);
        BigEndian.WriteUInt16(bytes, 0x1DE, 1);
        BigEndian.WriteUInt32(bytes, ContentsOffset, content.Id);
        BigEndian.WriteUInt16(bytes, ContentsOffset + 4, content.Index);
        BigEndian.WriteUInt16(bytes, ContentsOffset + 6, content.Type);
        BigEndian.WriteUInt64(bytes, ContentsOffset + 8, content.Size);
        content.Hash.CopyTo(bytes, ContentsOffset + 0x10);
        Signature.FakeSign(bytes, FakeSignOffset);
        return new Tmd(bytes);
    }

    /// <summary>
    /// Wraps TMD bytes.
    /// </summary>
    /// <param name="bytes">Header and content records.</param>
    public static Tmd Parse(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length < ContentsOffset)
            throw new ArgumentException("TMD is shorter than its header.", nameof(bytes));
        if (bytes.Length != ContentsOffset + BigEndian.ReadUInt16(bytes, 0x1DE) * ContentRecordSize)
            throw new InvalidDataException("TMD length does not match its content count.");

        return new Tmd((byte[])bytes.Clone());
    }

    /// <summary>
    /// Copy of the bytes.
    /// </summary>
    public byte[] ToBytes() => (byte[])_bytes.Clone();
}
