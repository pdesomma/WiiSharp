namespace WiiSharp;

/// <summary>
/// The four partition groups at 0x40000 and the partitions they list.
/// </summary>
public static class PartitionTable
{
    /// <summary>
    /// Bytes of the group table.
    /// </summary>
    public const int Size = DiscFormat.PartitionGroups * 8;

    /// <summary>
    /// Reads every partition entry, in table order.
    /// </summary>
    /// <param name="disc">Seekable disc image.</param>
    public static IReadOnlyList<PartitionEntry> Read(Stream disc)
    {
        if (disc is null)
            throw new ArgumentNullException(nameof(disc));

        var groups = disc.ReadExactlyAt(DiscFormat.PartitionTableOffset, Size);
        var entries = new List<PartitionEntry>();
        for (var g = 0; g < DiscFormat.PartitionGroups; g++)
        {
            var count = BigEndian.ReadUInt32(groups, g * 8);
            if (count == 0)
                continue;

            var tableOffset = (long)BigEndian.ReadUInt32(groups, g * 8 + 4) << 2;
            var table = disc.ReadExactlyAt(tableOffset, checked((int)(count * DiscFormat.PartitionEntrySize)));
            for (var i = 0; i < count; i++)
            {
                var at = i * DiscFormat.PartitionEntrySize;
                entries.Add(new PartitionEntry(
                    (long)BigEndian.ReadUInt32(table, at) << 2,
                    (PartitionType)BigEndian.ReadUInt32(table, at + 4)));
            }
        }
        return entries;
    }
}
