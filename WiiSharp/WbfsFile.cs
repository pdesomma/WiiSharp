namespace WiiSharp;

/// <summary>
/// An open WBFS container, split parts included.
/// </summary>
public sealed class WbfsFile : IDisposable
{
    private bool _disposed;

    private WbfsFile(IReadOnlyList<string> parts, SplitFileSource source, WbfsHeader header)
    {
        Parts = parts;
        Source = source;
        Header = header;
        Discs = ReadDiscs();
    }

    /// <summary>
    /// Discs present, in slot order.
    /// </summary>
    public IReadOnlyList<WbfsDisc> Discs { get; }
    /// <summary>
    /// Container geometry and disc table.
    /// </summary>
    public WbfsHeader Header { get; }
    /// <summary>
    /// Files that make up the container.
    /// </summary>
    public IReadOnlyList<string> Parts { get; }

    internal SplitFileSource Source { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        Source.Dispose();
    }

    /// <summary>
    /// Opens a .wbfs and any .wbs1, .wbs2 … parts beside it.
    /// </summary>
    /// <param name="path">Path of the .wbfs file.</param>
    /// <exception cref="InvalidDataException">Not a WBFS container.</exception>
    public static WbfsFile Open(string path)
    {
        var parts = PartPaths(path);
        var source = new SplitFileSource(parts);
        try
        {
            var first = new byte[4096];
            var read = source.ReadAt(0, first, 0, first.Length);
            var header = WbfsHeader.Parse(first.Take(read).ToArray());
            return new WbfsFile(parts, source, header);
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The .wbfs path followed by every consecutive .wbsN part that exists.
    /// </summary>
    /// <param name="path">Path of the .wbfs file.</param>
    /// <exception cref="FileNotFoundException">First part missing.</exception>
    public static IReadOnlyList<string> PartPaths(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException("WBFS file not found.", path);

        var parts = new List<string> { path };
        for (var i = 1; File.Exists(WbfsFormat.PartPath(path, i)); i++)
            parts.Add(WbfsFormat.PartPath(path, i));
        return parts;
    }

    internal void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(WbfsFile));
    }

    private IReadOnlyList<WbfsDisc> ReadDiscs()
    {
        var discs = new List<WbfsDisc>();
        var info = new byte[Header.DiscInfoSize];
        foreach (var slot in Header.DiscSlots)
        {
            var offset = Header.DiscInfoOffset(slot);
            if (offset + info.Length > Source.Length)
                break;
            if (Source.ReadAt(offset, info, 0, info.Length) != info.Length)
                throw new EndOfStreamException($"WBFS file ends inside disc slot {slot}.");

            var table = new ushort[Header.WbfsSectorsPerDisc];
            for (var i = 0; i < table.Length; i++)
                table[i] = (ushort)(info[WbfsFormat.DiscHeaderSize + i * 2] << 8 | info[WbfsFormat.DiscHeaderSize + i * 2 + 1]);
            discs.Add(new WbfsDisc(this, slot, DiscHeader.Parse(info), table));
        }
        return discs;
    }
}
