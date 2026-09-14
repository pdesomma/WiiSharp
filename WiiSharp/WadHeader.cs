namespace WiiSharp;

/// <summary>
/// The 0x20-byte header of a WAD: section sizes, each section then padded to 0x40.
/// </summary>
public sealed class WadHeader
{
    /// <summary>
    /// Section alignment.
    /// </summary>
    public const int Alignment = 0x40;
    /// <summary>
    /// Type of an installable WAD, the ASCII pair "Is".
    /// </summary>
    public const ushort InstallableType = 0x4973;
    /// <summary>
    /// Header length.
    /// </summary>
    public const int Size = 0x20;

    private WadHeader(ushort type, ushort version, uint certificateChainSize, uint ticketSize, uint tmdSize, ulong dataSize, uint footerSize)
    {
        Type = type;
        Version = version;
        CertificateChainSize = certificateChainSize;
        TicketSize = ticketSize;
        TmdSize = tmdSize;
        DataSize = dataSize;
        FooterSize = footerSize;
    }

    /// <summary>
    /// Bytes of certificate chain.
    /// </summary>
    public uint CertificateChainSize { get; }
    /// <summary>
    /// Offset of the certificate chain.
    /// </summary>
    public long CertificateChainOffset => Alignment;
    /// <summary>
    /// Bytes of encrypted content.
    /// </summary>
    public ulong DataSize { get; }
    /// <summary>
    /// Offset of the first content.
    /// </summary>
    public long DataOffset => Align(TmdOffset + TmdSize);
    /// <summary>
    /// Bytes of footer.
    /// </summary>
    public uint FooterSize { get; }
    /// <summary>
    /// Offset of the ticket.
    /// </summary>
    public long TicketOffset => Align(CertificateChainOffset + CertificateChainSize);
    /// <summary>
    /// Bytes of ticket.
    /// </summary>
    public uint TicketSize { get; }
    /// <summary>
    /// Offset of the TMD.
    /// </summary>
    public long TmdOffset => Align(TicketOffset + TicketSize);
    /// <summary>
    /// Bytes of TMD.
    /// </summary>
    public uint TmdSize { get; }
    /// <summary>
    /// WAD type, <see cref="InstallableType"/> for channels.
    /// </summary>
    public ushort Type { get; }
    /// <summary>
    /// Format version.
    /// </summary>
    public ushort Version { get; }

    /// <summary>
    /// Parses a header.
    /// </summary>
    /// <param name="bytes">At least <see cref="Size"/> bytes.</param>
    /// <exception cref="InvalidDataException">Not a WAD header.</exception>
    public static WadHeader Parse(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length < Size)
            throw new ArgumentException($"Need at least {Size} bytes.", nameof(bytes));
        if (BigEndian.ReadUInt32(bytes, 0) != Size)
            throw new InvalidDataException("Not a WAD: header size is not 0x20.");

        var header = new WadHeader(
            BigEndian.ReadUInt16(bytes, 4),
            BigEndian.ReadUInt16(bytes, 6),
            BigEndian.ReadUInt32(bytes, 8),
            BigEndian.ReadUInt32(bytes, 0x10),
            BigEndian.ReadUInt32(bytes, 0x14),
            BigEndian.ReadUInt32(bytes, 0x18),
            BigEndian.ReadUInt32(bytes, 0x1C));
        if (header.TicketSize < Ticket.Size)
            throw new InvalidDataException("WAD ticket is too short.");
        return header;
    }

    private static long Align(long value) => (value + Alignment - 1) / Alignment * Alignment;
}
