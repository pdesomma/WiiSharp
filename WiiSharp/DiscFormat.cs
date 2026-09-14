namespace WiiSharp;

/// <summary>
/// Fixed numbers of the Wii disc layout.
/// </summary>
public static class DiscFormat
{
    /// <summary>
    /// Bytes per encrypted cluster.
    /// </summary>
    public const int ClusterSize = 0x8000;
    /// <summary>
    /// Bytes of hashes at the start of each cluster.
    /// </summary>
    public const int ClusterHashSize = 0x400;
    /// <summary>
    /// Offset within the encrypted hash block of the IV for the cluster data.
    /// </summary>
    public const int ClusterIvOffset = 0x3D0;
    /// <summary>
    /// Bytes of payload in each cluster.
    /// </summary>
    public const int ClusterDataSize = ClusterSize - ClusterHashSize;
    /// <summary>
    /// Magic at 0x18 identifying a Wii disc.
    /// </summary>
    public const uint WiiMagic = 0x5D1C9EA3;
    /// <summary>
    /// Magic at 0x1C identifying a GameCube disc.
    /// </summary>
    public const uint GameCubeMagic = 0xC2339F3D;
    /// <summary>
    /// Offset of the partition table.
    /// </summary>
    public const long PartitionTableOffset = 0x40000;
    /// <summary>
    /// Partition groups in the table.
    /// </summary>
    public const int PartitionGroups = 4;
    /// <summary>
    /// Bytes per entry in a group's partition list.
    /// </summary>
    public const int PartitionEntrySize = 8;
    /// <summary>
    /// Key length in bytes.
    /// </summary>
    public const int KeySize = 16;
}
