namespace WiiSharp;

/// <summary>
/// A DOL held in memory so sections can be added and bytes patched by the address they load at.
/// </summary>
public sealed class DolFile
{
    private const int SectionSlots = DolHeader.TextSectionCount + DolHeader.DataSectionCount;

    private readonly uint[] _addresses = new uint[SectionSlots];
    private readonly byte[][] _contents = new byte[SectionSlots][];

    private DolFile(byte[] header, byte[] bytes)
    {
        for (var i = 0; i < SectionSlots; i++)
        {
            var offset = BigEndian.ReadUInt32(header, i * 4);
            var size = BigEndian.ReadUInt32(header, 0x90 + i * 4);
            _addresses[i] = BigEndian.ReadUInt32(header, 0x48 + i * 4);
            if (size == 0)
                continue;
            if (offset + (long)size > bytes.Length)
                throw new InvalidDataException($"DOL section {i} runs past the end of the file.");
            _contents[i] = new byte[size];
            Array.Copy(bytes, offset, _contents[i], 0, size);
        }
        BssAddress = BigEndian.ReadUInt32(header, 0xD8);
        BssSize = BigEndian.ReadUInt32(header, 0xDC);
        EntryPoint = BigEndian.ReadUInt32(header, 0xE0);
    }

    /// <summary>
    /// Where the zero-initialised area starts.
    /// </summary>
    public uint BssAddress { get; set; }
    /// <summary>
    /// Length of the zero-initialised area.
    /// </summary>
    public uint BssSize { get; set; }
    /// <summary>
    /// Address execution starts at.
    /// </summary>
    public uint EntryPoint { get; set; }
    /// <summary>
    /// The sections in slot order: text slots first, then data; empty slots skipped.
    /// </summary>
    public IEnumerable<(int Slot, bool IsText, uint Address, byte[] Content)> Sections =>
        Enumerable.Range(0, SectionSlots).Where(i => _contents[i] is not null).Select(i => (i, i < DolHeader.TextSectionCount, _addresses[i], _contents[i]));

    /// <summary>
    /// Parses a DOL.
    /// </summary>
    /// <param name="bytes">Whole file.</param>
    /// <exception cref="InvalidDataException">A section runs past the file.</exception>
    public static DolFile Parse(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length < DolHeader.Size)
            throw new ArgumentException($"Need at least {DolHeader.Size} bytes.", nameof(bytes));

        DolHeader.Parse(bytes);
        return new DolFile(bytes, bytes);
    }

    /// <summary>
    /// Adds a section in the first free slot of its kind.
    /// </summary>
    /// <param name="address">Where it loads.</param>
    /// <param name="content">Its bytes.</param>
    /// <param name="text">True for a text (code) section.</param>
    /// <returns>The slot used.</returns>
    /// <exception cref="InvalidOperationException">No free slot, or the range overlaps a section already there.</exception>
    public int AddSection(uint address, byte[] content, bool text)
    {
        if (content is null)
            throw new ArgumentNullException(nameof(content));
        if (content.Length == 0)
            throw new ArgumentException("A section holds at least one byte.", nameof(content));
        if (Sections.Any(s => address < s.Address + (ulong)s.Content.Length && s.Address < address + (ulong)content.Length))
            throw new InvalidOperationException($"A section already covers 0x{address:X8}.");

        var first = text ? 0 : DolHeader.TextSectionCount;
        var count = text ? DolHeader.TextSectionCount : DolHeader.DataSectionCount;
        for (var i = first; i < first + count; i++)
        {
            if (_contents[i] is not null)
                continue;
            _addresses[i] = address;
            _contents[i] = (byte[])content.Clone();
            return i;
        }
        throw new InvalidOperationException($"No free {(text ? "text" : "data")} section slot.");
    }

    /// <summary>
    /// Address of the first match of <paramref name="pattern"/> at or after <paramref name="start"/>, looking only inside sections; word-aligned when <paramref name="alignment"/> says so.
    /// </summary>
    /// <param name="pattern">Bytes to find.</param>
    /// <param name="start">Lowest address to consider.</param>
    /// <param name="alignment">Step between candidates.</param>
    /// <returns>Null when nothing matches.</returns>
    public uint? Find(byte[] pattern, uint start = 0, int alignment = 4)
    {
        if (pattern is null)
            throw new ArgumentNullException(nameof(pattern));
        if (pattern.Length == 0)
            throw new ArgumentException("Pattern must not be empty.", nameof(pattern));
        if (alignment <= 0)
            throw new ArgumentOutOfRangeException(nameof(alignment));

        foreach (var section in Sections.OrderBy(s => s.Address))
        {
            var content = section.Content;
            var from = Math.Max(0L, (long)start - section.Address);
            from = (from + alignment - 1) / alignment * alignment;
            for (var i = from; i + pattern.Length <= content.Length; i += alignment)
            {
                var hit = true;
                for (var j = 0; j < pattern.Length && hit; j++)
                    hit = content[i + j] == pattern[j];
                if (hit)
                    return (uint)(section.Address + i);
            }
        }
        return null;
    }

    /// <summary>
    /// Bytes at an address, read across one section.
    /// </summary>
    /// <param name="address">Where to read.</param>
    /// <param name="count">Bytes to read.</param>
    /// <exception cref="ArgumentOutOfRangeException">The range is not inside one section.</exception>
    public byte[] Read(uint address, int count)
    {
        var (section, offset) = Locate(address, count);
        var bytes = new byte[count];
        Array.Copy(section, offset, bytes, 0, count);
        return bytes;
    }

    /// <summary>
    /// Serialises the DOL: header, then every section in slot order.
    /// </summary>
    public byte[] ToBytes()
    {
        var header = new byte[DolHeader.Size];
        var output = new MemoryStream();
        output.Write(header, 0, header.Length);
        for (var i = 0; i < SectionSlots; i++)
        {
            var content = _contents[i];
            if (content is null)
                continue;
            var offset = (uint)output.Length;
            output.Write(content, 0, content.Length);
            while (output.Length % 4 != 0)
                output.WriteByte(0);
            BigEndian.WriteUInt32(header, i * 4, offset);
            BigEndian.WriteUInt32(header, 0x48 + i * 4, _addresses[i]);
            BigEndian.WriteUInt32(header, 0x90 + i * 4, (uint)content.Length);
        }
        BigEndian.WriteUInt32(header, 0xD8, BssAddress);
        BigEndian.WriteUInt32(header, 0xDC, BssSize);
        BigEndian.WriteUInt32(header, 0xE0, EntryPoint);
        var bytes = output.ToArray();
        header.CopyTo(bytes, 0);
        return bytes;
    }

    /// <summary>
    /// Overwrites bytes at an address inside one section.
    /// </summary>
    /// <param name="address">Where to write.</param>
    /// <param name="bytes">New bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">The range is not inside one section.</exception>
    public void Write(uint address, byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));

        var (section, offset) = Locate(address, bytes.Length);
        Array.Copy(bytes, 0, section, offset, bytes.Length);
    }

    private (byte[] Section, int Offset) Locate(uint address, int count)
    {
        for (var i = 0; i < SectionSlots; i++)
        {
            var content = _contents[i];
            if (content is null || address < _addresses[i] || address + (ulong)count > _addresses[i] + (ulong)content.Length)
                continue;
            return (content, (int)(address - _addresses[i]));
        }
        throw new ArgumentOutOfRangeException(nameof(address), address, $"0x{address:X8}+{count} is not inside one section.");
    }
}
