namespace WiiSharp;

/// <summary>
/// What <see cref="WiiDiscBuilder.Build"/> produced.
/// </summary>
/// <param name="Partition">Range from the partition start to the end of its data, the span a container must protect.</param>
/// <param name="Length">Bytes written.</param>
/// <param name="Ticket">Ticket stored at the partition start.</param>
/// <param name="Tmd">TMD stored in the partition header.</param>
public sealed record WiiDiscBuildResult(DataRange Partition, long Length, Ticket Ticket, Tmd Tmd);
