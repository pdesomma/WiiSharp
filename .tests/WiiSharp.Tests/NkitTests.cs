using System.Text;

namespace WiiSharp.Tests;

[TestClass]
public class NkitTests
{
    [TestMethod]
    public void JunkGenerator_Fill_MatchesKnownStreams()
    {
        var buffer = new byte[16];
        new JunkGenerator(Encoding.ASCII.GetBytes("GALE"), 0).Fill(0x600000, buffer, 0, 16);
        CollectionAssert.AreEqual(new byte[] { 0xE9, 0x47, 0x67, 0xBD, 0x41, 0x50, 0x4D, 0x5D, 0x61, 0x48, 0xB1, 0x99, 0xA0, 0x12, 0x0C, 0xBA }, buffer);

        new JunkGenerator(Encoding.ASCII.GetBytes("GALE"), 0).Fill(0x608000, buffer, 0, 16);
        CollectionAssert.AreEqual(new byte[] { 0xE2, 0xBB, 0xBD, 0x77, 0xDA, 0xB2, 0x22, 0x42, 0x1C, 0x0C, 0x0B, 0xFC, 0xAC, 0x06, 0xEA, 0xD0 }, buffer);

        new JunkGenerator(Encoding.ASCII.GetBytes("GPIE"), 0).Fill(0x322904, buffer, 0, 16);
        CollectionAssert.AreEqual(new byte[] { 0x97, 0xD8, 0x23, 0x0B, 0x12, 0xAA, 0x20, 0x45, 0xC2, 0xBD, 0x71, 0x8C, 0x30, 0x32, 0xC5, 0x2F }, buffer);
    }

    [TestMethod]
    public void JunkGenerator_Fill_ReseedsAtSectorBoundary()
    {
        var buffer = new byte[32];
        new JunkGenerator(Encoding.ASCII.GetBytes("GM8E"), 0).Fill(0x27FF0, buffer, 0, 32);

        CollectionAssert.AreEqual(new byte[]
        {
            0xAD, 0x6F, 0x21, 0xBE, 0x05, 0x57, 0x10, 0xED, 0xEA, 0xB0, 0x8E, 0xFD, 0x91, 0x58, 0xA2, 0x0E,
            0xDC, 0x0D, 0x59, 0xC0, 0x02, 0x98, 0xA5, 0x00, 0x39, 0x5B, 0x68, 0xA6, 0x5D, 0x53, 0x2D, 0xB6,
        }, buffer);
    }

    [TestMethod]
    public void JunkGenerator_Fill_AnyPositionEqualsSequentialStream()
    {
        var generator = new JunkGenerator(Encoding.ASCII.GetBytes("GLME"), 0);
        var whole = new byte[0x12000];
        generator.Fill(0x1F000, whole, 0, whole.Length);

        var piece = new byte[0x3000];
        generator.Fill(0x1F000 + 0x7F00, piece, 0, piece.Length);
        CollectionAssert.AreEqual(whole.Skip(0x7F00).Take(0x3000).ToArray(), piece, "seeking back inside a sector");

        generator.Fill(0x1F000 + 0x11FF0, piece, 0, 16);
        CollectionAssert.AreEqual(whole.Skip(0x11FF0).Take(16).ToArray(), piece.Take(16).ToArray(), "jumping ahead");
    }

    [TestMethod]
    public void JunkGenerator_Match_CountsLeadingJunkBytes()
    {
        var generator = new JunkGenerator(Encoding.ASCII.GetBytes("GLME"), 0);
        var data = new byte[0x9000];
        generator.Fill(0x4000, data, 0, data.Length);
        data[0x8123] ^= 1;

        Assert.AreEqual(0x8123, generator.Match(0x4000, data, 0, data.Length));
        Assert.AreEqual(0x100, generator.Match(0x4100, data, 0x100, 0x100));
    }

    [TestMethod]
    public void JunkGenerator_InvalidArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new JunkGenerator(null!, 0));
        Assert.ThrowsExactly<ArgumentException>(() => new JunkGenerator(new byte[3], 0));
        var generator = new JunkGenerator(Encoding.ASCII.GetBytes("GLME"), 0);
        Assert.ThrowsExactly<ArgumentNullException>(() => generator.Fill(0, null!, 0, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => generator.Fill(-1, new byte[4], 0, 4));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => generator.Fill(0, new byte[4], 2, 4));
        Assert.ThrowsExactly<ArgumentNullException>(() => generator.Match(0, null!, 0, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => generator.Match(0, new byte[4], 0, 8));
    }

    [TestMethod]
    public void Crc32_Update_MatchesCheckValue()
    {
        var bytes = Encoding.ASCII.GetBytes("123456789");

        Assert.AreEqual(0xCBF43926u, Crc32.Finish(Crc32.Update(Crc32.Initial, bytes, 0, bytes.Length)));
        Assert.AreEqual(0xCBF43926u, Crc32.Finish(Crc32.Update(Crc32.Update(Crc32.Initial, bytes, 0, 4), bytes, 4, 5)), "feeding in pieces");
    }

    [TestMethod]
    public void Crc32_Rewind_UndoesUpdate()
    {
        var bytes = Encoding.ASCII.GetBytes("Luigi's Mansion");
        var register = Crc32.Update(Crc32.Initial, bytes, 0, bytes.Length);

        for (var i = bytes.Length - 1; i >= 0; i--)
            register = Crc32.Rewind(register, bytes[i]);

        Assert.AreEqual(Crc32.Initial, register);
    }

    [TestMethod]
    public void Crc32_Patch_ForcesTheFinalChecksum()
    {
        var data = Enumerable.Range(0, 1000).Select(i => (byte)(i * 31)).ToArray();
        const int at = 0x20C;
        const uint target = 0x0B0C339D;

        var before = Crc32.Update(Crc32.Initial, data, 0, at);
        var register = target ^ Crc32.Initial;
        for (var i = data.Length - 1; i >= at + 4; i--)
            register = Crc32.Rewind(register, data[i]);
        Crc32.Patch(before, register).CopyTo(data, at);

        Assert.AreEqual(target, Crc32.Finish(Crc32.Update(Crc32.Initial, data, 0, data.Length)));
    }

    [TestMethod]
    public void Crc32_InvalidArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Crc32.Update(0, null!, 0, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Crc32.Update(0, new byte[2], 1, 2));
    }

    [TestMethod]
    public void NkitHeader_WriteThenParse_RoundTrips()
    {
        var header = new byte[0x440];
        BigEndianWrite(header, 0x1C, DiscFormat.GameCubeMagic);
        new NkitHeader(isWii: false, 0x0B0C339D, 0x57058000, forcedJunkId: 0x474C4D45, updatePartitionCrc: 0x940B57BB).Write(header);

        Assert.IsTrue(NkitHeader.IsPresent(header));
        Assert.AreEqual("NKIT v01", Encoding.ASCII.GetString(header, 0x200, 8));
        var parsed = NkitHeader.Parse(header);
        Assert.IsFalse(parsed.IsWii);
        Assert.AreEqual(0x0B0C339Du, parsed.SourceCrc);
        Assert.AreEqual(0x57058000L, parsed.SourceLength);
        Assert.AreEqual(0x474C4D45u, parsed.ForcedJunkId);
        Assert.AreEqual(0x940B57BBu, parsed.UpdatePartitionCrc);
        Assert.AreEqual(0u, parsed.Patch);
    }

    [TestMethod]
    public void NkitHeader_WiiImage_StoresLengthInWords()
    {
        var header = new byte[0x440];
        BigEndianWrite(header, 0x18, DiscFormat.WiiMagic);
        new NkitHeader(isWii: true, 1, DiscFormat.SingleLayerSize).Write(header);

        Assert.AreEqual(0x46090000u, (uint)(header[0x210] << 24 | header[0x211] << 16 | header[0x212] << 8 | header[0x213]));
        Assert.AreEqual(DiscFormat.SingleLayerSize, NkitHeader.Parse(header).SourceLength);
    }

    [TestMethod]
    public void NkitHeader_InvalidInput_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => NkitHeader.IsPresent(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => NkitHeader.Parse(null!));
        Assert.ThrowsExactly<ArgumentException>(() => NkitHeader.Parse(new byte[0x100]));
        Assert.ThrowsExactly<InvalidDataException>(() => NkitHeader.Parse(new byte[0x440]), "no magic");
        var wrongVersion = new byte[0x440];
        Encoding.ASCII.GetBytes("NKIT v02").CopyTo(wrongVersion, 0x200);
        Assert.ThrowsExactly<InvalidDataException>(() => NkitHeader.Parse(wrongVersion));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new NkitHeader(false, 0, 0x100000000));
        Assert.ThrowsExactly<ArgumentNullException>(() => new NkitHeader(false, 0, 0).Write(null!));
        Assert.IsFalse(NkitHeader.IsPresent(new byte[0x200]));
    }

    [TestMethod]
    public void NkitGameCube_Compact_ShrinksEveryGapToARecord()
    {
        var image = FakeGameCubeImage.Build();
        using var output = new MemoryStream();

        var header = NkitGameCube.Compact(new MemoryStream(image), output);
        var compact = output.ToArray();

        Assert.AreEqual(FakeGameCubeImage.Length, header.SourceLength);
        Assert.AreEqual(Crc32.Finish(Crc32.Update(Crc32.Initial, image, 0, image.Length)), header.SourceCrc);
        Assert.AreEqual(header.SourceCrc, Crc32.Finish(Crc32.Update(Crc32.Initial, compact, 0, compact.Length)), "the patch makes the compact file checksum like the source");
        Assert.IsTrue(compact.Length < image.Length / 4, $"compact is {compact.Length} bytes");
        Assert.AreEqual(0, compact.Length % NkitGameCube.EndAlignment);
        Assert.IsTrue(NkitHeader.IsPresent(compact));
        CollectionAssert.AreEqual(image.Take(0x200).ToArray(), compact.Take(0x200).ToArray(), "header before the NKit block is untouched");

        var fst = Fst.Parse(compact.Skip(FakeGameCubeImage.FstOffset).Take((int)ReadUInt32(compact, 0x428)).ToArray());
        var files = fst.ToDictionary(f => f.Path, f => (Offset: f.Offset >> 2, f.Length));
        var fstEnd = FakeGameCubeImage.FstOffset + (int)ReadUInt32(compact, 0x428);
        var firstRecord = (fstEnd + 3) & ~3;
        Assert.AreEqual((uint)(0x3400 - firstRecord), ReadUInt32(compact, firstRecord), "all-junk record after the table");
        Assert.AreEqual(firstRecord + 4, files["junk.bin"].Offset);

        var zerosRecord = (files["junk.bin"].Offset + 1000 + 3) & ~3;
        Assert.AreEqual(1u, ReadUInt32(compact, (int)zerosRecord) & 3, "all-zero record");

        var mixedRecord = (int)(files["zeros.bin"].Offset + 0x210 + 3) & ~3;
        Assert.AreEqual(2u, ReadUInt32(compact, mixedRecord) & 3, "mixed record");
        Assert.AreEqual(0x00000003u, ReadUInt32(compact, mixedRecord + 4), "three junk blocks");
        Assert.AreEqual(0x40000001u, ReadUInt32(compact, mixedRecord + 8), "one preserved block");
        Assert.AreEqual(0x800003FFu, ReadUInt32(compact, mixedRecord + 12 + 256), "three 0xFF blocks");
        Assert.AreEqual(mixedRecord + 16 + 256, files["mixed.bin"].Offset);

        Assert.AreEqual(0L, files["aligned.bin"].Offset % NkitGameCube.PreservedAlignment, "32 KiB files keep their alignment");
        Assert.AreEqual(files["tiny.bin"].Offset + 0x1235 + 3 & ~3, files["next.bin"].Offset, "no record between adjacent files");
        foreach (var file in FakeGameCubeImage.Files)
            CollectionAssert.AreEqual(image.Skip((int)file.Offset).Take(file.Length).ToArray(), compact.Skip((int)files[file.Path].Offset).Take(file.Length).ToArray(), file.Path);
    }

    [TestMethod]
    public void NkitGameCube_Restore_ReproducesTheImage()
    {
        var image = FakeGameCubeImage.Build();
        using var compact = new MemoryStream();
        NkitGameCube.Compact(new MemoryStream(image), compact);
        using var restored = new MemoryStream();

        compact.Position = 0;
        NkitGameCube.Restore(compact, restored);

        CollectionAssert.AreEqual(image, restored.ToArray());
    }

    [TestMethod]
    public void NkitGameCube_Restore_CorruptImage_Throws()
    {
        var image = FakeGameCubeImage.Build();
        using var compact = new MemoryStream();
        NkitGameCube.Compact(new MemoryStream(image), compact);
        var bytes = compact.ToArray();
        bytes[0x3400] ^= 0x80;

        Assert.ThrowsExactly<InvalidDataException>(() => NkitGameCube.Restore(new MemoryStream(bytes), new MemoryStream()));
    }

    [TestMethod]
    public void NkitGameCube_InvalidInput_Throws()
    {
        var image = FakeGameCubeImage.Build();
        Assert.ThrowsExactly<ArgumentNullException>(() => NkitGameCube.Compact(null!, new MemoryStream()));
        Assert.ThrowsExactly<ArgumentNullException>(() => NkitGameCube.Compact(new MemoryStream(image), null!));
        Assert.ThrowsExactly<ArgumentException>(() => NkitGameCube.Compact(new MemoryStream(image), new MemoryStream(new byte[10], writable: false)));
        Assert.ThrowsExactly<InvalidDataException>(() => NkitGameCube.Compact(new MemoryStream(new byte[0x1000]), new MemoryStream()), "not GameCube");
        Assert.ThrowsExactly<InvalidDataException>(() => NkitGameCube.Restore(new MemoryStream(image), new MemoryStream()), "not NKit");
        Assert.ThrowsExactly<ArgumentNullException>(() => NkitGameCube.Restore(null!, new MemoryStream()));

        using var compact = new MemoryStream();
        NkitGameCube.Compact(new MemoryStream(image), compact);
        compact.Position = 0;
        Assert.ThrowsExactly<InvalidDataException>(() => NkitGameCube.Compact(compact, new MemoryStream()), "already NKit");
    }

    [TestMethod]
    public void PartitionDataStream_Bare_ReadsPayloadWithoutHashBlocks()
    {
        var disc = FakeDisc.Build(encrypted: false);
        var partition = WiiDisc.Read(new MemoryStream(disc)).DataPartitions[0];

        var bare = new PartitionDataStream(new MemoryStream(disc), partition, hashed: false);

        Assert.AreEqual(partition.Header.DataSize, bare.Length);
        var head = new byte[16];
        bare.Position = DiscFormat.ClusterSize - 8;
        Assert.AreEqual(16, bare.Read(head, 0, 16));
        CollectionAssert.AreEqual(disc.Skip((int)partition.DataStart + DiscFormat.ClusterSize - 8).Take(16).ToArray(), head, "straight through the cluster boundary");
    }

    private static void BigEndianWrite(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        (uint)(bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3]);
}
