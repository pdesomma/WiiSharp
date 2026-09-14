using System.Text;

namespace WiiSharp;

/// <summary>
/// The apploader stored at 0x2440 of partition data: a 0x20 header, code and a trailer.
/// </summary>
public sealed class Apploader
{
    /// <summary>
    /// Bytes before the code.
    /// </summary>
    public const int HeaderSize = 0x20;

    private readonly byte[] _bytes;

    private Apploader(byte[] bytes)
    {
        _bytes = bytes;
    }

    /// <summary>
    /// Build date from the header, e.g. 2010/10/01.
    /// </summary>
    public string Date => Encoding.ASCII.GetString(_bytes, 0, 0x10).TrimEnd('\0');
    /// <summary>
    /// Entry point address.
    /// </summary>
    public uint EntryPoint => BigEndian.ReadUInt32(_bytes, 0x10);
    /// <summary>
    /// Whole apploader length.
    /// </summary>
    public int Length => _bytes.Length;

    /// <summary>
    /// Length the header describes: header, code and trailer.
    /// </summary>
    /// <param name="header">At least <see cref="HeaderSize"/> bytes.</param>
    public static int LengthOf(byte[] header)
    {
        if (header is null)
            throw new ArgumentNullException(nameof(header));
        if (header.Length < HeaderSize)
            throw new ArgumentException($"Need at least {HeaderSize} bytes.", nameof(header));

        return checked((int)(HeaderSize + BigEndian.ReadUInt32(header, 0x14) + BigEndian.ReadUInt32(header, 0x18)));
    }

    /// <summary>
    /// Wraps a complete apploader.
    /// </summary>
    /// <param name="bytes">Header, code and trailer.</param>
    public static Apploader Parse(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length != LengthOf(bytes))
            throw new InvalidDataException("Apploader length does not match its header.");

        return new Apploader((byte[])bytes.Clone());
    }

    /// <summary>
    /// Reads the apploader from plaintext partition data.
    /// </summary>
    /// <param name="partitionData">Seekable payload view.</param>
    public static Apploader Read(Stream partitionData)
    {
        if (partitionData is null)
            throw new ArgumentNullException(nameof(partitionData));

        var header = partitionData.ReadExactlyAt(DiscFormat.ApploaderOffset, HeaderSize);
        var bytes = new byte[LengthOf(header)];
        header.CopyTo(bytes, 0);
        partitionData.ReadExactly(bytes, HeaderSize, bytes.Length - HeaderSize);
        return new Apploader(bytes);
    }

    /// <summary>
    /// Copy of the bytes.
    /// </summary>
    public byte[] ToBytes() => (byte[])_bytes.Clone();
}
