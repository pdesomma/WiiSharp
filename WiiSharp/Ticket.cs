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
    /// Offset of the encrypted title key.
    /// </summary>
    public const int EncryptedTitleKeyOffset = 0x1BF;
    /// <summary>
    /// Ticket length.
    /// </summary>
    public const int Size = 0x2A4;
    /// <summary>
    /// Offset of the title ID.
    /// </summary>
    public const int TitleIdOffset = 0x1DC;

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
    /// Eight-byte title ID; also the IV for the title key.
    /// </summary>
    public byte[] TitleId => Slice(TitleIdOffset, 8);

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
