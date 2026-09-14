namespace WiiSharp;

/// <summary>
/// One row of the partition table.
/// </summary>
/// <param name="Offset">Absolute offset of the partition.</param>
/// <param name="Type">Kind of partition.</param>
public readonly record struct PartitionEntry(long Offset, PartitionType Type);
