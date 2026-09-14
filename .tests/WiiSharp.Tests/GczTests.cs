using System.IO.Compression;

namespace WiiSharp.Tests;

[TestClass]
public class GczTests
{
    private const int BlockSize = 0x100;
    private const int DataSize = 0x250;

    [TestMethod]
    public void Read_ParsesTablesAndStreamDecodesEveryBlockKind()
    {
        var plain = Plain();
        var image = Build(plain, corruptBlock: -1);

        using var gcz = GczFile.Read(new MemoryStream(image));
        using var stream = gcz.OpenStream();

        Assert.AreEqual(0u, gcz.Header.SubType);
        Assert.AreEqual((ulong)DataSize, gcz.Header.DataSize);
        Assert.AreEqual((uint)BlockSize, gcz.Header.BlockSize);
        Assert.AreEqual(3u, gcz.Header.BlockCount);
        Assert.AreEqual(GczHeader.Size + 3 * 12, gcz.DataOffset);
        Assert.AreEqual(0UL, gcz.Pointers[0]);
        Assert.AreNotEqual(0UL, gcz.Pointers[1] & GczFile.StoredFlag, "second block stored");
        Assert.AreEqual(DataSize, stream.Length);
        var all = new byte[DataSize];
        Assert.AreEqual(DataSize, stream.Read(all, 0, all.Length));
        CollectionAssert.AreEqual(plain, all);
        Assert.AreEqual(0, stream.Read(all, 0, 1), "end of stream");
    }

    [TestMethod]
    public void Stream_SeekAcrossBlocks_ReadsTheRightBytes()
    {
        var plain = Plain();
        using var gcz = GczFile.Read(new MemoryStream(Build(plain, corruptBlock: -1)));
        using var stream = gcz.OpenStream();

        stream.Seek(0xF0, SeekOrigin.Begin);
        var span = new byte[0x120];
        Assert.AreEqual(span.Length, stream.Read(span, 0, span.Length));
        CollectionAssert.AreEqual(plain.Skip(0xF0).Take(0x120).ToArray(), span);
        stream.Seek(-0x10, SeekOrigin.End);
        var tail = new byte[0x40];
        Assert.AreEqual(0x10, stream.Read(tail, 0, tail.Length));
        CollectionAssert.AreEqual(plain.Skip(DataSize - 0x10).ToArray(), tail.Take(0x10).ToArray());
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(tail, 0, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => stream.Position = -1);
    }

    [TestMethod]
    public void Stream_CorruptBlock_ThrowsInvalidDataException()
    {
        using var gcz = GczFile.Read(new MemoryStream(Build(Plain(), corruptBlock: 2)));
        using var stream = gcz.OpenStream();
        var buffer = new byte[DataSize];

        stream.Seek(2 * BlockSize, SeekOrigin.Begin);
        Assert.ThrowsExactly<InvalidDataException>(() => stream.Read(buffer, 0, 1));
    }

    [TestMethod]
    public void Read_NotGczOrBadTables_Throw()
    {
        var image = Build(Plain(), corruptBlock: -1);
        image[0] ^= 0xFF;
        Assert.ThrowsExactly<InvalidDataException>(() => GczFile.Read(new MemoryStream(image)));

        var wrongCount = Build(Plain(), corruptBlock: -1);
        wrongCount[28] = 9;
        Assert.ThrowsExactly<InvalidDataException>(() => GczFile.Read(new MemoryStream(wrongCount)));

        Assert.ThrowsExactly<ArgumentNullException>(() => GczFile.Read(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => GczFile.Open(null!));
    }

    [TestMethod]
    public void Open_File_DecodesAndDisposesTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "WiiSharp.Tests", Guid.NewGuid().ToString("N") + ".gcz");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Build(Plain(), corruptBlock: -1));
        try
        {
            using (var gcz = GczFile.Open(path))
            using (var stream = gcz.OpenStream())
            {
                var head = new byte[8];
                Assert.AreEqual(8, stream.Read(head, 0, 8));
                CollectionAssert.AreEqual(Plain().Take(8).ToArray(), head);
            }
            File.Delete(path);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static byte[] Plain() => Enumerable.Range(0, DataSize).Select(i => (byte)(i * 11 + 3)).ToArray();

    private static byte[] Build(byte[] plain, int corruptBlock)
    {
        var blocks = new List<byte[]>();
        var pointers = new List<ulong>();
        var data = new MemoryStream();
        for (var i = 0; i < 3; i++)
        {
            var slice = plain.Skip(i * BlockSize).Take(BlockSize).ToArray();
            var stored = i == 1;
            var bytes = stored ? slice : Zlib(slice);
            pointers.Add((ulong)data.Length | (stored ? GczFile.StoredFlag : 0));
            data.Write(bytes, 0, bytes.Length);
            blocks.Add(bytes);
        }

        var image = new MemoryStream();
        image.Write(BitConverter.GetBytes(GczHeader.Magic), 0, 4);
        image.Write(BitConverter.GetBytes(0u), 0, 4);
        image.Write(BitConverter.GetBytes((ulong)data.Length), 0, 8);
        image.Write(BitConverter.GetBytes((ulong)DataSize), 0, 8);
        image.Write(BitConverter.GetBytes((uint)BlockSize), 0, 4);
        image.Write(BitConverter.GetBytes(3u), 0, 4);
        foreach (var pointer in pointers)
            image.Write(BitConverter.GetBytes(pointer), 0, 8);
        for (var i = 0; i < blocks.Count; i++)
            image.Write(BitConverter.GetBytes(Adler(blocks[i]) ^ (i == corruptBlock ? 1u : 0u)), 0, 4);
        data.WriteTo(image);
        return image.ToArray();
    }

    private static uint Adler(byte[] bytes)
    {
        uint a = 1, b = 0;
        foreach (var x in bytes)
        {
            a = (a + x) % 65521;
            b = (b + a) % 65521;
        }
        return b << 16 | a;
    }

    private static byte[] Zlib(byte[] plain)
    {
        var output = new MemoryStream();
        output.WriteByte(0x78);
        output.WriteByte(0x9C);
        using (var deflate = new DeflateStream(output, CompressionMode.Compress, leaveOpen: true))
            deflate.Write(plain, 0, plain.Length);
        var adler = Adler(plain);
        output.Write(new[] { (byte)(adler >> 24), (byte)(adler >> 16), (byte)(adler >> 8), (byte)adler }, 0, 4);
        return output.ToArray();
    }
}
