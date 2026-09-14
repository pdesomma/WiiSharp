namespace WiiSharp;

/// <summary>
/// A partition and where its encrypted data lives on the disc.
/// </summary>
public sealed class Partition
{
    internal Partition(PartitionEntry entry, Ticket ticket, PartitionHeader header)
    {
        Offset = entry.Offset;
        Type = entry.Type;
        Ticket = ticket;
        Header = header;
    }

    /// <summary>
    /// Absolute end of the encrypted data.
    /// </summary>
    public long DataEnd => DataStart + Header.DataSize;
    /// <summary>
    /// Absolute start of the encrypted data.
    /// </summary>
    public long DataStart => Offset + Header.DataOffset;
    /// <summary>
    /// Fields after the ticket.
    /// </summary>
    public PartitionHeader Header { get; }
    /// <summary>
    /// Absolute offset of the partition.
    /// </summary>
    public long Offset { get; }
    /// <summary>
    /// Ticket at the start of the partition.
    /// </summary>
    public Ticket Ticket { get; }
    /// <summary>
    /// Kind of partition.
    /// </summary>
    public PartitionType Type { get; }
}
