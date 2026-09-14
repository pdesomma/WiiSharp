namespace WiiSharp.Tests;

[TestClass]
public class WiiDiscTests
{
    [TestMethod]
    public void DataPartitionsExcludesTheUpdatePartition()
    {
        var disc = WiiDisc.Read(new MemoryStream(FakeDisc.Build(encrypted: true)));

        Assert.AreEqual(1, disc.DataPartitions.Count);
        Assert.AreEqual(FakeDisc.DataPartitionOffset, disc.DataPartitions[0].Offset);
        Assert.AreEqual(FakeDisc.DataStart, disc.DataPartitions[0].DataStart);
        Assert.AreEqual(FakeDisc.DataStart + FakeDisc.Clusters * DiscFormat.ClusterSize, disc.DataPartitions[0].DataEnd);
    }

    [TestMethod]
    public void ReadParsesHeaderAndPartitions()
    {
        var disc = WiiDisc.Read(new MemoryStream(FakeDisc.Build(encrypted: true)));

        Assert.AreEqual("RTESTE", disc.Header.GameId);
        Assert.AreEqual(1, disc.Header.DiscNumber);
        Assert.AreEqual(2, disc.Header.Version);
        Assert.AreEqual("Fake Test Disc", disc.Header.Title);
        Assert.IsTrue(disc.Header.IsWii);
        Assert.IsFalse(disc.Header.IsGameCube);

        Assert.AreEqual(2, disc.Partitions.Count);
        Assert.AreEqual(PartitionType.Update, disc.Partitions[0].Type);
        Assert.AreEqual(FakeDisc.UpdatePartitionOffset, disc.Partitions[0].Offset);
        Assert.AreEqual(PartitionType.Data, disc.Partitions[1].Type);
        Assert.AreEqual(0x20000, disc.Partitions[1].Header.DataOffset);
        Assert.AreEqual(FakeDisc.Clusters * DiscFormat.ClusterSize, disc.Partitions[1].Header.DataSize);
    }

    [TestMethod]
    public void ReadRejectsNonWiiOrNonSeekableInput()
    {
        var notWii = FakeDisc.Build(encrypted: true);
        notWii[0x18] = 0;

        Assert.ThrowsExactly<InvalidDataException>(() => WiiDisc.Read(new MemoryStream(notWii)));
        Assert.ThrowsExactly<ArgumentException>(() => WiiDisc.Read(new ForwardOnlyStream(FakeDisc.Build(encrypted: true))));
    }

    [TestMethod]
    public void TicketExposesKeyMaterial()
    {
        var disc = WiiDisc.Read(new MemoryStream(FakeDisc.Build(encrypted: true)));
        var ticket = disc.DataPartitions[0].Ticket;

        CollectionAssert.AreEqual(FakeDisc.TitleId, ticket.TitleId);
        Assert.AreEqual(0, ticket.CommonKeyIndex);
        Assert.AreEqual(16, ticket.EncryptedTitleKey.Length);
        Assert.AreEqual(Ticket.Size, ticket.ToBytes().Length);
    }

    private sealed class ForwardOnlyStream : MemoryStream
    {
        public ForwardOnlyStream(byte[] bytes) : base(bytes)
        {
        }

        public override bool CanSeek => false;
    }
}
