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
    /// Clusters sharing one H1 table.
    /// </summary>
    public const int SubgroupClusters = 8;
    /// <summary>
    /// Clusters sharing one H2 table and one H3 entry.
    /// </summary>
    public const int GroupClusters = SubgroupClusters * SubgroupClusters;
    /// <summary>
    /// Payload bytes in one hash group.
    /// </summary>
    public const int GroupDataSize = GroupClusters * ClusterDataSize;
    /// <summary>
    /// Magic at 0x18 identifying a Wii disc.
    /// </summary>
    public const uint WiiMagic = 0x5D1C9EA3;
    /// <summary>
    /// Magic at 0x1C identifying a GameCube disc.
    /// </summary>
    public const uint GameCubeMagic = 0xC2339F3D;
    /// <summary>
    /// Magic every retail disc carries at <see cref="DiscMagicOffset"/>.
    /// </summary>
    public const uint DiscMagic = 0xC3F81A8E;
    /// <summary>
    /// Offset of <see cref="DiscMagic"/>.
    /// </summary>
    public const long DiscMagicOffset = 0x4FFFC;
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
    /// Where retail single-layer discs place the game partition.
    /// </summary>
    public const long RetailDataPartitionOffset = 0xF800000;
    /// <summary>
    /// Bytes on a single-layer disc.
    /// </summary>
    public const long SingleLayerSize = 0x118240000;
    /// <summary>
    /// Offset of the H3 table within a partition.
    /// </summary>
    public const long H3Offset = 0x8000;
    /// <summary>
    /// Bytes reserved for the H3 table.
    /// </summary>
    public const int H3Size = 0x18000;
    /// <summary>
    /// Offset of the first cluster within a partition.
    /// </summary>
    public const long PartitionDataOffset = 0x20000;
    /// <summary>
    /// Bytes of boot.bin at the start of partition data.
    /// </summary>
    public const int BootSize = 0x440;
    /// <summary>
    /// Bytes of bi2.bin following boot.bin.
    /// </summary>
    public const int Bi2Size = 0x2000;
    /// <summary>
    /// Offset of the apploader within partition data.
    /// </summary>
    public const long ApploaderOffset = BootSize + Bi2Size;
    /// <summary>
    /// SHA-1 digest length.
    /// </summary>
    public const int HashSize = 20;
    /// <summary>
    /// Key length in bytes.
    /// </summary>
    public const int KeySize = 16;
}
