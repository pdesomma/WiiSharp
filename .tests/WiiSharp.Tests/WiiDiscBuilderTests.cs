using System.Security.Cryptography;
using System.Text;

namespace WiiSharp.Tests;

[TestClass]
public class WiiDiscBuilderTests
{
    private const long PartitionOffset = 0x50000;
    private static readonly byte[] Dol = Enumerable.Range(0, 0x1234).Select(i => (byte)(i * 7 + 1)).ToArray();
    private static readonly byte[] Game = Enumerable.Range(0, 0x9000).Select(i => (byte)(i * 3 + 5)).ToArray();
    private static readonly byte[] Disc2 = Enumerable.Range(0, 0x123).Select(i => (byte)(i + 9)).ToArray();

    [TestMethod]
    public void Build_TwoFiles_WritesLayoutTablesAndHashes()
    {
        var builder = Builder();
        builder.Files.Add(new DiscFile("game.iso", new MemoryStream(Game)));
        builder.Files.Add(new DiscFile("disc2.iso", new MemoryStream(Disc2)));
        var output = new MemoryStream();

        var result = builder.Build(output);

        var disc = WiiDisc.Read(output);
        Assert.AreEqual("GALE01", disc.Header.GameId);
        Assert.AreEqual("Melee", disc.Header.Title);
        Assert.IsTrue(disc.Header.IsWii);
        Assert.AreEqual(1, disc.Partitions.Count);
        var partition = disc.DataPartitions[0];
        Assert.AreEqual(PartitionOffset, partition.Offset);
        Assert.AreEqual(WiiDiscBuilder.TmdOffset, partition.Header.TmdOffset);
        Assert.AreEqual(0x4E0, partition.Header.CertificateChainOffset);
        Assert.AreEqual(0xA00u, partition.Header.CertificateChainSize);
        Assert.AreEqual(DiscFormat.H3Offset, partition.Header.H3Offset);
        Assert.AreEqual(DiscFormat.PartitionDataOffset, partition.Header.DataOffset);
        Assert.AreEqual(3L * DiscFormat.GroupClusters * DiscFormat.ClusterSize, partition.Header.DataSize, "disc2 ends in the third group");
        Assert.AreEqual(new DataRange(PartitionOffset, partition.DataEnd), result.Partition);
        Assert.AreEqual(output.Length, result.Length);
        Assert.AreEqual(partition.DataEnd, output.Length);

        Assert.AreEqual(DiscRegion.Europe, RegionArea.Read(output).Region);
        var magic = ReadAt(output, DiscFormat.DiscMagicOffset, 4);
        Assert.AreEqual(DiscFormat.DiscMagic, (uint)(magic[0] << 24 | magic[1] << 16 | magic[2] << 8 | magic[3]));

        CollectionAssert.AreEqual(result.Ticket.ToBytes(), partition.Ticket.ToBytes());
        CollectionAssert.AreEqual(new byte[] { 0, 1, 0, 0, 0x47, 0x41, 0x4C, 0x45 }, partition.Ticket.TitleId);
        var tmd = Tmd.Parse(ReadAt(output, PartitionOffset + WiiDiscBuilder.TmdOffset, (int)partition.Header.TmdSize));
        CollectionAssert.AreEqual(result.Tmd.ToBytes(), tmd.ToBytes());
        Assert.AreEqual((ushort)0x3031, tmd.GroupId);
        Assert.AreEqual(DiscRegion.Europe, tmd.Region);
        Assert.AreEqual((ulong)partition.Header.DataSize, tmd.Contents[0].Size);
        var h3 = ReadAt(output, partition.Offset + DiscFormat.H3Offset, DiscFormat.H3Size);
        using var sha1 = SHA1.Create();
        CollectionAssert.AreEqual(sha1.ComputeHash(h3), tmd.Contents[0].Hash);

        var system = PartitionSystemFiles.Read(output, partition);
        CollectionAssert.AreEqual(FakeSystemFiles.CertificateChain(), system.CertificateChain);
        CollectionAssert.AreEqual(FakeSystemFiles.Bi2(), system.Bi2);
        CollectionAssert.AreEqual(FakeSystemFiles.ApploaderBytes(), system.Apploader.ToBytes());
        var boot = system.Boot;
        Assert.AreEqual("GALE01", Encoding.ASCII.GetString(boot, 0, 6));
        Assert.AreEqual(1, boot[6]);
        Assert.AreEqual(2, boot[7]);
        Assert.AreEqual(FakeSystemFiles.UserPosition, ReadUInt32(boot, 0x430), "template fields past the offsets survive");

        var data = new PartitionDataStream(output, partition);
        var dolOffset = (long)ReadUInt32(boot, 0x420) << 2;
        var fstOffset = (long)ReadUInt32(boot, 0x424) << 2;
        var fstSize = (int)(ReadUInt32(boot, 0x428) << 2);
        Assert.AreEqual(fstSize, (int)(ReadUInt32(boot, 0x42C) << 2));
        Assert.AreEqual(0, fstSize % 4, "FST padded so the shifted size is exact");
        Assert.AreEqual(DiscFormat.ApploaderOffset + FakeSystemFiles.ApploaderBytes().Length, dolOffset, "apploader end is already aligned");
        CollectionAssert.AreEqual(Dol, ReadAt(data, dolOffset, Dol.Length));
        var files = Fst.Parse(ReadAt(data, fstOffset, fstSize));
        CollectionAssert.AreEqual(new[] { new FstFile("game.iso", 0x1F0000, Game.Length), new FstFile("disc2.iso", 0x3E0000, Disc2.Length) }, files.ToArray());
        Assert.IsTrue(fstSize >= Fst.Build(files).Length && fstSize < Fst.Build(files).Length + 4, "size is the table rounded up to 4");
        CollectionAssert.AreEqual(Game, ReadAt(data, 0x1F0000, Game.Length));
        CollectionAssert.AreEqual(Disc2, ReadAt(data, 0x3E0000, Disc2.Length));

        AssertHashTree(output, partition, sha1, h3);
    }

    [TestMethod]
    public void Build_NoFiles_StillWritesOneGroup()
    {
        var output = new MemoryStream();

        var result = Builder().Build(output);

        var partition = WiiDisc.Read(output).DataPartitions[0];
        Assert.AreEqual((long)DiscFormat.GroupClusters * DiscFormat.ClusterSize, partition.Header.DataSize);
        Assert.AreEqual(0UL, result.Tmd.Contents[0].Size % DiscFormat.ClusterSize);
        var boot = PartitionSystemFiles.Read(output, partition).Boot;
        var fst = ReadAt(new PartitionDataStream(output, partition), (long)ReadUInt32(boot, 0x424) << 2, (int)(ReadUInt32(boot, 0x428) << 2));
        Assert.AreEqual(0, Fst.Parse(fst).Count);
    }

    [TestMethod]
    public void Build_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(() => Builder().Build(new MemoryStream(), cancelled.Token));
    }

    [TestMethod]
    public void Build_PartitionPastTheDisc_ThrowsInvalidDataException()
    {
        var builder = new WiiDiscBuilder("GALE01", "Melee", FakeSystemFiles.Create(), Dol) { PartitionOffset = DiscFormat.SingleLayerSize - DiscFormat.PartitionDataOffset };
        var output = new MemoryStream();

        Assert.ThrowsExactly<InvalidDataException>(() => builder.Build(output));
        Assert.AreEqual(0, output.Length, "nothing written");
    }

    [TestMethod]
    public void Build_BadOutputOrSettings_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Builder().Build(null!));
        Assert.ThrowsExactly<ArgumentException>(() => Builder().Build(new MemoryStream(new byte[1], writable: false)));
        Assert.ThrowsExactly<InvalidOperationException>(() => new WiiDiscBuilder("GALE01", "M", FakeSystemFiles.Create(), Dol) { FileAlignment = 6 }.Build(new MemoryStream()));
        Assert.ThrowsExactly<InvalidOperationException>(() => new WiiDiscBuilder("GALE01", "M", FakeSystemFiles.Create(), Dol) { PartitionOffset = 0x1000 }.Build(new MemoryStream()));
    }

    [TestMethod]
    public void Constructor_BadArguments_Throw()
    {
        var system = FakeSystemFiles.Create();

        Assert.ThrowsExactly<ArgumentNullException>(() => new WiiDiscBuilder(null!, "M", system, Dol));
        Assert.ThrowsExactly<ArgumentException>(() => new WiiDiscBuilder("GALE", "M", system, Dol));
        Assert.ThrowsExactly<ArgumentException>(() => new WiiDiscBuilder("GALE01", new string('x', 65), system, Dol));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WiiDiscBuilder("GALE01", "M", null!, Dol));
        Assert.ThrowsExactly<ArgumentNullException>(() => new WiiDiscBuilder("GALE01", "M", system, null!));
        Assert.ThrowsExactly<ArgumentException>(() => new WiiDiscBuilder("GALE01", "M", system, Array.Empty<byte>()));
    }

    [TestMethod]
    public void Apploader_ParseAndLengthOf_FollowTheHeader()
    {
        var bytes = FakeSystemFiles.ApploaderBytes();

        var apploader = Apploader.Parse(bytes);

        Assert.AreEqual(bytes.Length, Apploader.LengthOf(bytes));
        Assert.AreEqual("2026/01/01", apploader.Date);
        Assert.AreEqual(0x81200000u, apploader.EntryPoint);
        Assert.ThrowsExactly<InvalidDataException>(() => Apploader.Parse(bytes.Take(bytes.Length - 1).ToArray()));
        Assert.ThrowsExactly<ArgumentException>(() => Apploader.LengthOf(new byte[4]));
        Assert.ThrowsExactly<ArgumentNullException>(() => Apploader.Parse(null!));
    }

    [TestMethod]
    public void PartitionSystemFiles_BadArguments_Throw()
    {
        var apploader = Apploader.Parse(FakeSystemFiles.ApploaderBytes());

        Assert.ThrowsExactly<ArgumentException>(() => new PartitionSystemFiles(new byte[1], FakeSystemFiles.Bi2(), apploader, FakeSystemFiles.CertificateChain()));
        Assert.ThrowsExactly<ArgumentException>(() => new PartitionSystemFiles(FakeSystemFiles.Boot(), new byte[1], apploader, FakeSystemFiles.CertificateChain()));
        Assert.ThrowsExactly<ArgumentNullException>(() => new PartitionSystemFiles(FakeSystemFiles.Boot(), FakeSystemFiles.Bi2(), null!, FakeSystemFiles.CertificateChain()));
        Assert.ThrowsExactly<ArgumentException>(() => new PartitionSystemFiles(FakeSystemFiles.Boot(), FakeSystemFiles.Bi2(), apploader, Array.Empty<byte>()));
        Assert.ThrowsExactly<ArgumentNullException>(() => PartitionSystemFiles.Read(null!, null!));
    }

    [TestMethod]
    public void HashGroup_Layout_MatchesRetailClusters()
    {
        // verified against a retail disc: H0 at 0, H1 at 0x280, H2 at 0x340, the IV inside H2 at 0x3D0
        Assert.AreEqual(0x280, HashGroup.H1Offset);
        Assert.AreEqual(0x340, HashGroup.H2Offset);
        Assert.IsTrue(DiscFormat.ClusterIvOffset > HashGroup.H2Offset && DiscFormat.ClusterIvOffset < HashGroup.H2Offset + HashGroup.H1Size);

        var data = Enumerable.Range(0, DiscFormat.GroupDataSize).Select(i => (byte)(i * 7)).ToArray();
        var clusters = new byte[DiscFormat.GroupClusters * DiscFormat.ClusterSize];
        HashGroup.Write(data, clusters);

        Assert.IsTrue(clusters.Skip(0x320).Take(0x20).All(x => x == 0), "gap between H1 and H2 is zero");
        Assert.IsTrue(clusters.Skip(0x340).Take(20).Any(x => x != 0), "H2 starts at 0x340");
        Assert.IsTrue(clusters.Skip(0x3E0).Take(0x20).All(x => x == 0), "padding after H2 is zero");
        CollectionAssert.AreEqual(clusters.Skip(0x340).Take(0xA0).ToArray(), clusters.Skip(DiscFormat.ClusterSize + 0x340).Take(0xA0).ToArray(), "every cluster carries the group's H2");
    }

    private static void AssertHashTree(Stream output, Partition partition, SHA1 sha1, byte[] h3)
    {
        var groups = partition.Header.DataSize / DiscFormat.ClusterSize / DiscFormat.GroupClusters;
        for (var g = 0; g < groups; g++)
        {
            var first = partition.DataStart + g * DiscFormat.GroupClusters * (long)DiscFormat.ClusterSize;
            var h2 = ReadAt(output, first + HashGroup.H2Offset, HashGroup.H1Size);
            CollectionAssert.AreEqual(sha1.ComputeHash(h2), h3.Skip(g * DiscFormat.HashSize).Take(DiscFormat.HashSize).ToArray(), $"H3[{g}]");
            for (var s = 0; s < DiscFormat.SubgroupClusters; s++)
            {
                var subgroupFirst = first + s * DiscFormat.SubgroupClusters * (long)DiscFormat.ClusterSize;
                var h1 = ReadAt(output, subgroupFirst + HashGroup.H1Offset, HashGroup.H1Size);
                CollectionAssert.AreEqual(sha1.ComputeHash(h1), h2.Skip(s * DiscFormat.HashSize).Take(DiscFormat.HashSize).ToArray(), $"H2[{g},{s}]");
                for (var c = 0; c < DiscFormat.SubgroupClusters; c++)
                {
                    var cluster = ReadAt(output, subgroupFirst + c * DiscFormat.ClusterSize, DiscFormat.ClusterSize);
                    CollectionAssert.AreEqual(sha1.ComputeHash(cluster, 0, HashGroup.H0Size), h1.Skip(c * DiscFormat.HashSize).Take(DiscFormat.HashSize).ToArray(), $"H1[{g},{s},{c}]");
                    for (var b = 0; b < 31; b++)
                        CollectionAssert.AreEqual(sha1.ComputeHash(cluster, DiscFormat.ClusterHashSize + b * DiscFormat.ClusterHashSize, DiscFormat.ClusterHashSize), cluster.Skip(b * DiscFormat.HashSize).Take(DiscFormat.HashSize).ToArray(), $"H0[{g},{s},{c},{b}]");
                    Assert.IsTrue(cluster.Skip(HashGroup.H0Size).Take(HashGroup.H1Offset - HashGroup.H0Size).All(x => x == 0), "H0 padding is zero");
                    Assert.IsTrue(cluster.Skip(HashGroup.H2Offset + HashGroup.H1Size).Take(DiscFormat.ClusterHashSize - HashGroup.H2Offset - HashGroup.H1Size).All(x => x == 0), "H2 padding is zero");
                }
            }
        }
    }

    private static WiiDiscBuilder Builder() =>
        new("GALE01", "Melee", FakeSystemFiles.Create(), Dol)
        {
            DiscNumber = 1,
            Version = 2,
            PartitionOffset = PartitionOffset,
            Region = RegionSettings.Preset(DiscRegion.Europe),
        };

    private static byte[] ReadAt(Stream stream, long position, int count)
    {
        var bytes = new byte[count];
        stream.Position = position;
        var read = 0;
        while (read < count)
        {
            var n = stream.Read(bytes, read, count - read);
            Assert.AreNotEqual(0, n, "unexpected end of stream");
            read += n;
        }
        return bytes;
    }

    private static uint ReadUInt32(byte[] bytes, int offset) => (uint)(bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3]);
}
