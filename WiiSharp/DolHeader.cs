namespace WiiSharp;

/// <summary>
/// The 0x100-byte header of a DOL executable: seven text and eleven data sections, BSS and entry point.
/// </summary>
public sealed class DolHeader
{
    /// <summary>
    /// Data sections a DOL can carry.
    /// </summary>
    public const int DataSectionCount = 11;
    /// <summary>
    /// Header length.
    /// </summary>
    public const int Size = 0x100;
    /// <summary>
    /// Text sections a DOL can carry.
    /// </summary>
    public const int TextSectionCount = 7;

    private DolHeader(IReadOnlyList<DolSection> text, IReadOnlyList<DolSection> data, uint bssAddress, uint bssSize, uint entryPoint)
    {
        TextSections = text;
        DataSections = data;
        BssAddress = bssAddress;
        BssSize = bssSize;
        EntryPoint = entryPoint;
    }

    /// <summary>
    /// Address of the zero-filled region.
    /// </summary>
    public uint BssAddress { get; }
    /// <summary>
    /// Bytes to zero.
    /// </summary>
    public uint BssSize { get; }
    /// <summary>
    /// Data sections that have bytes, in header order.
    /// </summary>
    public IReadOnlyList<DolSection> DataSections { get; }
    /// <summary>
    /// First instruction.
    /// </summary>
    public uint EntryPoint { get; }
    /// <summary>
    /// Bytes the file occupies: the end of its furthest section.
    /// </summary>
    public long Length => Math.Max(Size, TextSections.Concat(DataSections).Select(s => s.End).DefaultIfEmpty(0).Max());
    /// <summary>
    /// Text sections that have bytes, in header order.
    /// </summary>
    public IReadOnlyList<DolSection> TextSections { get; }

    /// <summary>
    /// Parses a header.
    /// </summary>
    /// <param name="bytes">At least <see cref="Size"/> bytes.</param>
    /// <exception cref="InvalidDataException">No section has bytes.</exception>
    public static DolHeader Parse(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length < Size)
            throw new ArgumentException($"Need at least {Size} bytes.", nameof(bytes));

        var text = Sections(bytes, 0, TextSectionCount);
        var data = Sections(bytes, TextSectionCount, DataSectionCount);
        if (text.Count + data.Count == 0)
            throw new InvalidDataException("DOL header describes no sections.");

        return new DolHeader(text, data, BigEndian.ReadUInt32(bytes, 0xD8), BigEndian.ReadUInt32(bytes, 0xDC), BigEndian.ReadUInt32(bytes, 0xE0));
    }

    private static IReadOnlyList<DolSection> Sections(byte[] bytes, int first, int count)
    {
        var sections = new List<DolSection>();
        for (var i = first; i < first + count; i++)
        {
            var size = BigEndian.ReadUInt32(bytes, 0x90 + i * 4);
            if (size == 0)
                continue;
            sections.Add(new DolSection(BigEndian.ReadUInt32(bytes, i * 4), BigEndian.ReadUInt32(bytes, 0x48 + i * 4), size));
        }
        return sections;
    }
}
