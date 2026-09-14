namespace WiiSharp;

/// <summary>
/// Layout of a Wii disc image: header and partitions.
/// </summary>
public sealed class WiiDisc
{
    private WiiDisc(DiscHeader header, IReadOnlyList<Partition> partitions)
    {
        Header = header;
        Partitions = partitions;
    }

    /// <summary>
    /// Game partitions in disc order.
    /// </summary>
    public IReadOnlyList<Partition> DataPartitions =>
        Partitions.Where(p => p.Type == PartitionType.Data).OrderBy(p => p.Offset).ToArray();
    /// <summary>
    /// Disc header.
    /// </summary>
    public DiscHeader Header { get; }
    /// <summary>
    /// Every partition, in table order.
    /// </summary>
    public IReadOnlyList<Partition> Partitions { get; }

    /// <summary>
    /// Reads the layout without touching partition data.
    /// </summary>
    /// <param name="disc">Seekable disc image.</param>
    /// <exception cref="InvalidDataException">Not a Wii disc.</exception>
    public static WiiDisc Read(Stream disc)
    {
        if (disc is null)
            throw new ArgumentNullException(nameof(disc));
        if (!disc.CanSeek)
            throw new ArgumentException("Disc image must be seekable.", nameof(disc));

        var header = DiscHeader.Parse(disc.ReadExactlyAt(0, DiscHeader.Size));
        if (!header.IsWii)
            throw new InvalidDataException("Not a Wii disc image.");

        var partitions = new List<Partition>();
        foreach (var entry in PartitionTable.Read(disc))
        {
            var ticket = Ticket.Parse(disc.ReadExactlyAt(entry.Offset, Ticket.Size));
            var partitionHeader = PartitionHeader.Parse(disc.ReadExactly(PartitionHeader.Size));
            partitions.Add(new Partition(entry, ticket, partitionHeader));
        }
        return new WiiDisc(header, partitions);
    }
}
