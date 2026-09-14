using System.Security.Cryptography;

namespace WiiSharp;

/// <summary>
/// Builds the H0–H2 tables of one 64-cluster group and yields its H3 entry.
/// </summary>
public static class HashGroup
{
    /// <summary>
    /// Offset of the H1 table inside a cluster.
    /// </summary>
    public const int H1Offset = 0x280;
    /// <summary>
    /// Offset of the H2 table inside a cluster.
    /// </summary>
    public const int H2Offset = 0x320;
    /// <summary>
    /// Bytes of the H0 table: 31 hashes.
    /// </summary>
    public const int H0Size = 31 * DiscFormat.HashSize;
    /// <summary>
    /// Bytes of an H1 or H2 table: 8 hashes.
    /// </summary>
    public const int H1Size = DiscFormat.SubgroupClusters * DiscFormat.HashSize;

    /// <summary>
    /// Lays one group of payload out as hashed clusters.
    /// </summary>
    /// <param name="data">Exactly <see cref="DiscFormat.GroupDataSize"/> bytes.</param>
    /// <param name="clusters">Receives <see cref="DiscFormat.GroupClusters"/> clusters.</param>
    /// <returns>SHA-1 of the H2 table, the group's H3 entry.</returns>
    public static byte[] Write(byte[] data, byte[] clusters)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data));
        if (data.Length != DiscFormat.GroupDataSize)
            throw new ArgumentException($"A group holds {DiscFormat.GroupDataSize} bytes.", nameof(data));
        if (clusters is null)
            throw new ArgumentNullException(nameof(clusters));
        if (clusters.Length != DiscFormat.GroupClusters * DiscFormat.ClusterSize)
            throw new ArgumentException($"A group is {DiscFormat.GroupClusters} clusters.", nameof(clusters));

        using var sha1 = SHA1.Create();
        Array.Clear(clusters, 0, clusters.Length);
        var h2 = new byte[H1Size];
        for (var subgroup = 0; subgroup < DiscFormat.SubgroupClusters; subgroup++)
        {
            var h1 = new byte[H1Size];
            for (var c = 0; c < DiscFormat.SubgroupClusters; c++)
            {
                var cluster = subgroup * DiscFormat.SubgroupClusters + c;
                var at = cluster * DiscFormat.ClusterSize;
                Array.Copy(data, cluster * DiscFormat.ClusterDataSize, clusters, at + DiscFormat.ClusterHashSize, DiscFormat.ClusterDataSize);
                for (var block = 0; block < 31; block++)
                    sha1.ComputeHash(clusters, at + DiscFormat.ClusterHashSize + block * DiscFormat.ClusterHashSize, DiscFormat.ClusterHashSize).CopyTo(clusters, at + block * DiscFormat.HashSize);
                sha1.ComputeHash(clusters, at, H0Size).CopyTo(h1, c * DiscFormat.HashSize);
            }
            for (var c = 0; c < DiscFormat.SubgroupClusters; c++)
                h1.CopyTo(clusters, (subgroup * DiscFormat.SubgroupClusters + c) * DiscFormat.ClusterSize + H1Offset);
            sha1.ComputeHash(h1).CopyTo(h2, subgroup * DiscFormat.HashSize);
        }
        for (var cluster = 0; cluster < DiscFormat.GroupClusters; cluster++)
            h2.CopyTo(clusters, cluster * DiscFormat.ClusterSize + H2Offset);
        return sha1.ComputeHash(h2);
    }
}
