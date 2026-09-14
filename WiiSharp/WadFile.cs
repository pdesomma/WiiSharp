namespace WiiSharp;

/// <summary>
/// The parts of a WAD read without decrypting anything: header, ticket and TMD.
/// </summary>
public sealed class WadFile
{
    private WadFile(WadHeader header, Ticket ticket, Tmd tmd)
    {
        Header = header;
        Ticket = ticket;
        Tmd = tmd;
    }

    /// <summary>
    /// Header.
    /// </summary>
    public WadHeader Header { get; }
    /// <summary>
    /// Ticket.
    /// </summary>
    public Ticket Ticket { get; }
    /// <summary>
    /// Eight-byte title ID from the ticket.
    /// </summary>
    public byte[] TitleId => Ticket.TitleId;
    /// <summary>
    /// TMD.
    /// </summary>
    public Tmd Tmd { get; }

    /// <summary>
    /// Reads a WAD file.
    /// </summary>
    /// <param name="path">.wad path.</param>
    public static WadFile Open(string path)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        using var stream = File.OpenRead(path);
        return Read(stream);
    }

    /// <summary>
    /// Reads the header, ticket and TMD.
    /// </summary>
    /// <param name="stream">Seekable WAD.</param>
    public static WadFile Read(Stream stream)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));
        if (!stream.CanSeek)
            throw new ArgumentException("WAD must be seekable.", nameof(stream));

        var header = WadHeader.Parse(stream.ReadExactlyAt(0, WadHeader.Size));
        var ticket = Ticket.Parse(stream.ReadExactlyAt(header.TicketOffset, Ticket.Size));
        var tmd = Tmd.Parse(stream.ReadExactlyAt(header.TmdOffset, checked((int)header.TmdSize)));
        return new WadFile(header, ticket, tmd);
    }
}
