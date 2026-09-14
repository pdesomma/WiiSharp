namespace WiiSharp;

/// <summary>
/// First sector of a WBFS container: geometry and the disc table.
/// </summary>
public sealed class WbfsHeader
{
    private readonly bool[] _discTable;

    private WbfsHeader(uint hdSectorCount, int hdSectorShift, int wbfsSectorShift, bool[] discTable)
    {
        HdSectorCount = hdSectorCount;
        HdSectorShift = hdSectorShift;
        WbfsSectorShift = wbfsSectorShift;
        _discTable = discTable;
    }

    /// <summary>
    /// Bytes each disc slot takes: header copy plus sector table, rounded to an hd sector.
    /// </summary>
    public int DiscInfoSize => (WbfsFormat.DiscHeaderSize + WbfsSectorsPerDisc * 2 + HdSectorSize - 1) / HdSectorSize * HdSectorSize;
    /// <summary>
    /// Slots that hold a disc.
    /// </summary>
    public IReadOnlyList<int> DiscSlots => _discTable.Select((present, i) => (present, i)).Where(p => p.present).Select(p => p.i).ToArray();
    /// <summary>
    /// Sectors on the device the container was made for.
    /// </summary>
    public uint HdSectorCount { get; }
    /// <summary>
    /// Log2 of the device sector size, normally 9.
    /// </summary>
    public int HdSectorShift { get; }
    /// <summary>
    /// Device sector size in bytes.
    /// </summary>
    public int HdSectorSize => 1 << HdSectorShift;
    /// <summary>
    /// Slots the disc table has room for.
    /// </summary>
    public int MaxDiscs => _discTable.Length;
    /// <summary>
    /// Log2 of the allocation unit, normally 21 (2 MB).
    /// </summary>
    public int WbfsSectorShift { get; }
    /// <summary>
    /// Allocation unit in bytes.
    /// </summary>
    public long WbfsSectorSize => 1L << WbfsSectorShift;
    /// <summary>
    /// Entries in each disc's sector table.
    /// </summary>
    public int WbfsSectorsPerDisc => WbfsFormat.WiiSectorsPerDisc >> (WbfsSectorShift - WbfsFormat.WiiSectorShift);

    /// <summary>
    /// Byte offset of a disc slot.
    /// </summary>
    /// <param name="slot">Zero-based slot.</param>
    public long DiscInfoOffset(int slot)
    {
        if (slot < 0 || slot >= MaxDiscs)
            throw new ArgumentOutOfRangeException(nameof(slot));

        return HdSectorSize + (long)slot * DiscInfoSize;
    }

    /// <summary>
    /// Parses the first device sector.
    /// </summary>
    /// <param name="bytes">At least the first hd sector.</param>
    /// <exception cref="InvalidDataException">Bad magic or geometry.</exception>
    public static WbfsHeader Parse(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length < WbfsFormat.HeaderSize)
            throw new ArgumentException($"Need at least {WbfsFormat.HeaderSize} bytes.", nameof(bytes));
        if (BigEndian.ReadUInt32(bytes, 0) != WbfsFormat.Magic)
            throw new InvalidDataException("Not a WBFS file.");

        var hdShift = bytes[8];
        var wbfsShift = bytes[9];
        if (hdShift < 9 || hdShift > 12 || wbfsShift < WbfsFormat.WiiSectorShift || wbfsShift > 31 || wbfsShift <= hdShift)
            throw new InvalidDataException($"Unsupported WBFS geometry: hd 2^{hdShift}, wbfs 2^{wbfsShift}.");

        var hdSectorSize = 1 << hdShift;
        if (bytes.Length < hdSectorSize)
            throw new ArgumentException($"Need the whole first sector ({hdSectorSize} bytes).", nameof(bytes));

        var table = new bool[hdSectorSize - WbfsFormat.HeaderSize];
        for (var i = 0; i < table.Length; i++)
            table[i] = bytes[WbfsFormat.HeaderSize + i] != 0;
        return new WbfsHeader(BigEndian.ReadUInt32(bytes, 4), hdShift, wbfsShift, table);
    }
}
