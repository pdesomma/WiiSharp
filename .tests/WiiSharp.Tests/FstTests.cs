using System.Text;

namespace WiiSharp.Tests;

[TestClass]
public class FstTests
{
    [TestMethod]
    public void Build_NestedPaths_RoundTripsThroughParse()
    {
        var files = new[]
        {
            new FstFile("game.iso", 0x1F0000, 1000),
            new FstFile("sound/bgm.brstm", 0x3E0000, 4),
            new FstFile("sound/fx/hit.wav", 0x3E0004, 8),
            new FstFile("disc2.iso", 0x5D0000, 2000),
        };

        var bytes = Fst.Build(files);
        var parsed = Fst.Parse(bytes);

        CollectionAssert.AreEqual(files, parsed.ToArray());
        Assert.AreEqual(1, bytes[0], "root is a directory");
        Assert.AreEqual(7u, (uint)(bytes[8] << 24 | bytes[9] << 16 | bytes[10] << 8 | bytes[11]), "root, 4 files, 2 directories");
    }

    [TestMethod]
    public void Build_RootOnly_HasSingleEntryAndEmptyName()
    {
        var bytes = Fst.Build(Array.Empty<FstFile>());

        Assert.AreEqual(Fst.EntrySize + 1, bytes.Length);
        Assert.AreEqual(0, Fst.Parse(bytes).Count);
    }

    [TestMethod]
    public void Build_StoresOffsetsShiftedByTwo()
    {
        var bytes = Fst.Build(new[] { new FstFile("a", 0x100, 5) });

        Assert.AreEqual(0x40u, (uint)(bytes[16] << 24 | bytes[17] << 16 | bytes[18] << 8 | bytes[19]));
        Assert.AreEqual("a", Encoding.ASCII.GetString(bytes, 2 * Fst.EntrySize + 1, 1));
    }

    [TestMethod]
    public void Build_UnalignedOffsetOrEmptyPath_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Fst.Build(new[] { new FstFile("a", 3, 1) }));
        Assert.ThrowsExactly<ArgumentException>(() => Fst.Build(new[] { new FstFile("", 0, 1) }));
        Assert.ThrowsExactly<ArgumentNullException>(() => Fst.Build(null!));
    }

    [TestMethod]
    public void Parse_TruncatedTable_ThrowsInvalidDataException()
    {
        var bytes = Fst.Build(new[] { new FstFile("dir/a", 0, 1) });
        bytes[11] = 9;

        Assert.ThrowsExactly<InvalidDataException>(() => Fst.Parse(bytes));
        Assert.ThrowsExactly<InvalidDataException>(() => Fst.Parse(new byte[4]));
        Assert.ThrowsExactly<ArgumentNullException>(() => Fst.Parse(null!));
    }
}
