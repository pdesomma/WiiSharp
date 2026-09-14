namespace WiiSharp;

/// <summary>
/// The ticket at the start of a partition; carries the encrypted title key.
/// </summary>
public sealed class Ticket
{
    /// <summary>
    /// Offset of the common key index.
    /// </summary>
    public const int CommonKeyIndexOffset = 0x1F1;
    /// <summary>
    /// Offset of the 64-byte content access mask.
    /// </summary>
    public const int ContentAccessOffset = 0x222;
    /// <summary>
    /// Bytes in the content access mask.
    /// </summary>
    public const int ContentAccessSize = 0x40;
    /// <summary>
    /// Offset of the encrypted title key.
    /// </summary>
    public const int EncryptedTitleKeyOffset = 0x1BF;
    /// <summary>
    /// Issuer of disc tickets.
    /// </summary>
    public const string Issuer = "Root-CA00000001-XS00000003";
    /// <summary>
    /// Ticket length.
    /// </summary>
    public const int Size = 0x2A4;
    /// <summary>
    /// Offset of the ticket ID.
    /// </summary>
    public const int TicketIdOffset = 0x1D0;
    /// <summary>
    /// Offset of the title ID.
    /// </summary>
    public const int TitleIdOffset = 0x1DC;

    private const int FakeSignOffset = 0x262;

    private readonly byte[] _bytes;

    private Ticket(byte[] bytes)
    {
        _bytes = bytes;
    }

    /// <summary>
    /// Which common key decrypts the title key: 0 retail, 1 Korean, 2 vWii.
    /// </summary>
    public byte CommonKeyIndex => _bytes[CommonKeyIndexOffset];
    /// <summary>
    /// Title key as stored, encrypted with the common key.
    /// </summary>
    public byte[] EncryptedTitleKey => Slice(EncryptedTitleKeyOffset, DiscFormat.KeySize);
    /// <summary>
    /// Ticket ID; a launchable disc ticket needs a non-zero one.
    /// </summary>
    public ulong TicketId => BigEndian.ReadUInt64(_bytes, TicketIdOffset);
    /// <summary>
    /// Eight-byte title ID; also the IV for the title key.
    /// </summary>
    public byte[] TitleId => Slice(TitleIdOffset, 8);

    /// <summary>
    /// Builds an unsigned, fakesigned disc ticket granting access to every content, common key index 0.
    /// </summary>
    /// <param name="titleId">Eight-byte title ID.</param>
    /// <param name="encryptedTitleKey">Sixteen bytes stored as the title key.</param>
    public static Ticket Build(byte[] titleId, byte[] encryptedTitleKey)
    {
        if (titleId is null)
            throw new ArgumentNullException(nameof(titleId));
        if (titleId.Length != 8)
            throw new ArgumentException("A title ID is 8 bytes.", nameof(titleId));
        if (encryptedTitleKey is null)
            throw new ArgumentNullException(nameof(encryptedTitleKey));
        if (encryptedTitleKey.Length != DiscFormat.KeySize)
            throw new ArgumentException($"A title key is {DiscFormat.KeySize} bytes.", nameof(encryptedTitleKey));

        var bytes = new byte[Size];
        BigEndian.WriteUInt32(bytes, 0, Signature.Rsa2048Type);
        Signature.WriteIssuer(bytes, Issuer);
        encryptedTitleKey.CopyTo(bytes, EncryptedTitleKeyOffset);
        BigEndian.WriteUInt64(bytes, TicketIdOffset, 0x0001000000000000UL | BigEndian.ReadUInt32(titleId, 4));
        titleId.CopyTo(bytes, TitleIdOffset);
        BigEndian.WriteUInt16(bytes, 0x1E4, 0xFFFF);
        for (var i = 0; i < ContentAccessSize; i++)
            bytes[ContentAccessOffset + i] = 0xFF;
        Signature.FakeSign(bytes, FakeSignOffset);
        return new Ticket(bytes);
    }

    /// <summary>
    /// Wraps ticket bytes.
    /// </summary>
    /// <param name="bytes">Exactly <see cref="Size"/> bytes.</param>
    public static Ticket Parse(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length != Size)
            throw new ArgumentException($"A ticket is {Size} bytes.", nameof(bytes));

        return new Ticket((byte[])bytes.Clone());
    }

    /// <summary>
    /// Copy of the ticket bytes.
    /// </summary>
    public byte[] ToBytes() => (byte[])_bytes.Clone();

    private byte[] Slice(int offset, int length)
    {
        var result = new byte[length];
        Array.Copy(_bytes, offset, result, 0, length);
        return result;
    }
}
