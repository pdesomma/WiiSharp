namespace WiiSharp.Tests;

[TestClass]
public class WbfsTests
{
    private const long Sector = 1L << FakeWbfs.WbfsShift;

    private string _root = null!;

    [TestInitialize]
    public void Initialize() => _root = Path.Combine(Path.GetTempPath(), "WiiSharp.Tests", Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public void Header_Parse_ReadsGeometryAndDiscTable()
    {
        var header = WbfsHeader.Parse(FakeWbfs.Build(FakeDisc.Build(encrypted: true), FakeDisc.Build(encrypted: true)).Take(512).ToArray());

        Assert.AreEqual(9, header.HdSectorShift);
        Assert.AreEqual(512, header.HdSectorSize);
        Assert.AreEqual(18, header.WbfsSectorShift);
        Assert.AreEqual(Sector, header.WbfsSectorSize);
        Assert.AreEqual(WbfsFormat.WiiSectorsPerDisc / 8, header.WbfsSectorsPerDisc);
        Assert.AreEqual(72192, header.DiscInfoSize);
        Assert.AreEqual(500, header.MaxDiscs);
        CollectionAssert.AreEqual(new[] { 0, 1 }, header.DiscSlots.ToArray());
        Assert.AreEqual(512L, header.DiscInfoOffset(0));
        Assert.AreEqual(512L + 72192, header.DiscInfoOffset(1));
    }

    [TestMethod]
    public void Header_Parse_BadMagic_ThrowsInvalidDataException()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => WbfsHeader.Parse(new byte[512]));
    }

    [TestMethod]
    public void Header_Parse_BadGeometry_ThrowsInvalidDataException()
    {
        var bytes = FakeWbfs.Build(FakeDisc.Build(encrypted: true)).Take(512).ToArray();
        bytes[9] = 8;

        Assert.ThrowsExactly<InvalidDataException>(() => WbfsHeader.Parse(bytes));
    }

    [TestMethod]
    public void Open_SingleFile_ReadsDiscHeaderAndSectorTable()
    {
        var iso = FakeDisc.Build(encrypted: true);
        var path = FakeWbfs.Write(_root, FakeWbfs.Build(iso));

        using var wbfs = WbfsFile.Open(path);

        Assert.AreEqual(1, wbfs.Parts.Count);
        var disc = wbfs.Discs.Single();
        Assert.AreEqual(0, disc.Slot);
        Assert.AreEqual("RTESTE", disc.Header.GameId);
        Assert.AreEqual("Fake Test Disc", disc.Header.Title);
        Assert.IsTrue(disc.Header.IsWii);
        var sectors = (iso.Length + Sector - 1) / Sector;
        CollectionAssert.AreEqual(Enumerable.Range(1, (int)sectors).Select(i => (ushort)i).ToArray(), disc.SectorTable.Take((int)sectors).ToArray());
        Assert.AreEqual(0, disc.SectorTable[(int)sectors]);
        Assert.AreEqual(sectors * Sector, disc.StoredLength);
    }

    [TestMethod]
    public void OpenStream_SingleFile_ReadsBackTheIso()
    {
        var iso = FakeDisc.Build(encrypted: true);
        var path = FakeWbfs.Write(_root, FakeWbfs.Build(iso));

        using var wbfs = WbfsFile.Open(path);
        using var stream = wbfs.Discs[0].OpenStream();

        Assert.IsTrue(stream.CanSeek);
        var back = new byte[iso.Length];
        stream.Position = 0;
        ReadAll(stream, back);
        CollectionAssert.AreEqual(iso, back);
        var rest = new byte[stream.Length - iso.Length];
        ReadAll(stream, rest);
        CollectionAssert.AreEqual(new byte[rest.Length], rest, "tail of the last sector is zero");
        Assert.AreEqual(0, stream.Read(new byte[16], 0, 16), "end of stream");
    }

    [TestMethod]
    public void OpenStream_UnstoredSector_ReadsZeros()
    {
        var iso = FakeDisc.Build(encrypted: true);
        var padded = new byte[iso.Length + 2 * Sector];
        iso.CopyTo(padded, 0);
        padded[padded.Length - 1] = 0xAB;
        var path = FakeWbfs.Write(_root, FakeWbfs.Build(padded));

        using var wbfs = WbfsFile.Open(path);
        var disc = wbfs.Discs[0];
        using var stream = disc.OpenStream();

        var storedBefore = (int)((iso.Length + Sector - 1) / Sector);
        Assert.AreEqual(0, disc.SectorTable[storedBefore], "the all-zero sector is not stored");
        Assert.AreNotEqual(0, disc.SectorTable[storedBefore + 1]);
        var back = new byte[padded.Length];
        ReadAll(stream, back);
        CollectionAssert.AreEqual(padded, back);
    }

    [TestMethod]
    public void OpenStream_SeekIntoMiddle_ReadsAcrossSectorBoundary()
    {
        var iso = FakeDisc.Build(encrypted: true);
        var path = FakeWbfs.Write(_root, FakeWbfs.Build(iso));

        using var wbfs = WbfsFile.Open(path);
        using var stream = wbfs.Discs[0].OpenStream();

        var buffer = new byte[64];
        stream.Seek(Sector - 32, SeekOrigin.Begin);
        ReadAll(stream, buffer);
        CollectionAssert.AreEqual(iso.Skip((int)Sector - 32).Take(64).ToArray(), buffer);
        stream.Seek(-16, SeekOrigin.End);
        Assert.AreEqual(stream.Length - 16, stream.Position);
    }

    [TestMethod]
    public void OpenStream_WiiDiscRead_SeesThePartitions()
    {
        var path = FakeWbfs.Write(_root, FakeWbfs.Build(FakeDisc.Build(encrypted: true)));

        using var wbfs = WbfsFile.Open(path);
        using var stream = wbfs.Discs[0].OpenStream();
        var disc = WiiDisc.Read(stream);

        Assert.AreEqual(2, disc.Partitions.Count);
        Assert.AreEqual(FakeDisc.DataPartitionOffset, disc.DataPartitions[0].Offset);
    }

    [TestMethod]
    public void Open_SplitParts_ReadsAcrossFiles()
    {
        var iso = FakeDisc.Build(encrypted: true);
        var path = FakeWbfs.Write(_root, FakeWbfs.Build(iso), splitAt: Sector + 12345);

        using var wbfs = WbfsFile.Open(path);
        using var stream = wbfs.Discs[0].OpenStream();

        Assert.IsTrue(wbfs.Parts.Count >= 2, "split into parts");
        StringAssert.EndsWith(wbfs.Parts[1], ".wbs1");
        var back = new byte[iso.Length];
        ReadAll(stream, back);
        CollectionAssert.AreEqual(iso, back);
    }

    [TestMethod]
    public void Open_TwoDiscs_ReadsBoth()
    {
        var first = FakeDisc.Build(encrypted: true);
        var second = FakeDisc.Build(encrypted: false);
        var path = FakeWbfs.Write(_root, FakeWbfs.Build(first, second));

        using var wbfs = WbfsFile.Open(path);

        Assert.AreEqual(2, wbfs.Discs.Count);
        Assert.AreEqual(1, wbfs.Discs[1].Slot);
        using var stream = wbfs.Discs[1].OpenStream();
        var back = new byte[second.Length];
        ReadAll(stream, back);
        CollectionAssert.AreEqual(second, back);
    }

    [TestMethod]
    public void Open_MissingFile_ThrowsFileNotFoundException()
    {
        Assert.ThrowsExactly<FileNotFoundException>(() => WbfsFile.Open(Path.Combine(_root, "nope.wbfs")));
    }

    [TestMethod]
    public void Open_NotWbfs_ThrowsInvalidDataException()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "game.wbfs");
        File.WriteAllBytes(path, FakeDisc.Build(encrypted: true));

        Assert.ThrowsExactly<InvalidDataException>(() => WbfsFile.Open(path));
    }

    [TestMethod]
    public void OpenStream_AfterDispose_ThrowsObjectDisposedException()
    {
        var path = FakeWbfs.Write(_root, FakeWbfs.Build(FakeDisc.Build(encrypted: true)));
        var wbfs = WbfsFile.Open(path);
        var stream = wbfs.Discs[0].OpenStream();
        wbfs.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => stream.Read(new byte[16], 0, 16));
    }

    [TestMethod]
    public void PartPath_Parts_NamesWbfsThenWbsN()
    {
        Assert.AreEqual(@"C:\x\game.wbfs", WbfsFormat.PartPath(@"C:\x\game.wbfs", 0));
        Assert.AreEqual(@"C:\x\game.wbs1", WbfsFormat.PartPath(@"C:\x\game.wbfs", 1));
        Assert.AreEqual(@"C:\x\game.wbs12", WbfsFormat.PartPath(@"C:\x\game.wbfs", 12));
    }

    private static void ReadAll(Stream stream, byte[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
                throw new EndOfStreamException();
            total += read;
        }
    }
}
