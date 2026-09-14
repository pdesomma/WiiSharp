namespace WiiSharp;

/// <summary>
/// One disc in a WBFS container.
/// </summary>
public sealed class WbfsDisc
{
    private readonly WbfsFile _file;
    private readonly ushort[] _sectorTable;

    internal WbfsDisc(WbfsFile file, int slot, DiscHeader header, ushort[] sectorTable)
    {
        _file = file;
        Slot = slot;
        Header = header;
        _sectorTable = sectorTable;
    }

    /// <summary>
    /// Disc header copy: game ID, title, magic.
    /// </summary>
    public DiscHeader Header { get; }
    /// <summary>
    /// Slot in the container.
    /// </summary>
    public int Slot { get; }
    /// <summary>
    /// Container sector holding each disc sector, or 0 where nothing is stored.
    /// </summary>
    public IReadOnlyList<ushort> SectorTable => _sectorTable;
    /// <summary>
    /// Bytes of the disc up to the last stored sector.
    /// </summary>
    public long StoredLength
    {
        get
        {
            var last = -1;
            for (var i = 0; i < _sectorTable.Length; i++)
                if (_sectorTable[i] != 0)
                    last = i;
            return (long)(last + 1) << _file.Header.WbfsSectorShift;
        }
    }

    /// <summary>
    /// Seekable read-only view of the disc image; unstored sectors read as zeros.
    /// </summary>
    public Stream OpenStream() => new WbfsDiscStream(_file, this);

    internal int ReadAt(long position, byte[] buffer, int index, int count)
    {
        var shift = _file.Header.WbfsSectorShift;
        var sector = (int)(position >> shift);
        if (sector >= _sectorTable.Length)
            return 0;

        var within = position & ((1L << shift) - 1);
        var chunk = (int)Math.Min(count, (1L << shift) - within);
        var stored = _sectorTable[sector];
        if (stored == 0)
        {
            Array.Clear(buffer, index, chunk);
            return chunk;
        }

        var read = _file.Source.ReadAt(((long)stored << shift) + within, buffer, index, chunk);
        if (read < chunk)
            throw new EndOfStreamException($"WBFS file ends inside sector {stored}.");
        return chunk;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Header.GameId} {Header.Title}";
}
