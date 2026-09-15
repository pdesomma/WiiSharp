using System.Text;

namespace WiiSharp.Tests;

/// <summary>
/// Small GameCube image whose gaps exercise every NKit record kind.
/// </summary>
internal static class FakeGameCubeImage
{
    public const string GameId = "GTSTE0";
    public const int FstOffset = 0x3000;
    public const long Length = 0x80000;

    public static readonly (string Path, long Offset, int Length)[] Files =
    {
        ("junk.bin", 0x3400, 1000),           // 28 nulls then junk before it
        ("zeros.bin", 0x3B00, 0x210),         // all-zero gap before it
        ("mixed.bin", 0x4400, 300),           // junk, odd bytes, then 0xFF fill before it
        ("aligned.bin", 0x10000, 0x8000),     // 32 KiB aligned and sized, junk before it
        ("tiny.bin", 0x18004, 0x1235),        // four-byte gap, which the rule fills with nulls
        ("next.bin", 0x1923C, 0x100),         // no gap at all
        ("unaligned.bin", 0x1A000, 0x7FFF),   // odd length ahead of the tail junk
    };

    public static byte[] Build()
    {
        var image = new byte[Length];
        Encoding.ASCII.GetBytes(GameId).CopyTo(image, 0);
        WriteUInt32(image, 0x1C, DiscFormat.GameCubeMagic);
        Encoding.ASCII.GetBytes("Fake Cube Disc").CopyTo(image, 0x20);
        for (var i = DiscFormat.BootSize; i < FstOffset; i++)
            image[i] = (byte)(i * 13);

        var fst = Fst.Build(Files.Select(f => new FstFile(f.Path, f.Offset << 2, f.Length)).ToArray());
        WriteUInt32(image, 0x420, 0x2440);
        WriteUInt32(image, 0x424, FstOffset);
        WriteUInt32(image, 0x428, (uint)fst.Length);
        WriteUInt32(image, 0x42C, (uint)fst.Length);
        fst.CopyTo(image, FstOffset);

        var junk = new JunkGenerator(image, 0);
        void Junk(long from, long to) => junk.Fill(from, image, (int)from, (int)(to - from));
        void Nulls(long from, int count) => Array.Clear(image, (int)from, count);
        foreach (var file in Files)
        {
            for (var i = 0; i < file.Length; i++)
                image[file.Offset + i] = (byte)(file.Path[0] + i);
        }

        var fstEnd = Align4(FstOffset + fst.Length);
        Nulls(fstEnd, 28);
        Junk(fstEnd + 28, 0x3400);

        var zerosGap = Align4(0x3400 + 1000);
        Nulls(zerosGap, (int)(0x3B00 - zerosGap));

        var mixedGap = Align4(0x3B00 + 0x210);
        Nulls(mixedGap, 28);
        Junk(mixedGap + 28, mixedGap + 0x300);
        for (var i = mixedGap + 0x300; i < mixedGap + 0x400; i++)
            image[i] = (byte)(0x55 ^ i);
        for (var i = mixedGap + 0x400; i < 0x4400; i++)
            image[i] = 0xFF;

        var alignedGap = Align4(0x4400 + 300);
        Nulls(alignedGap, 28);
        Junk(alignedGap + 28, 0x10000);

        Nulls(0x18000, 4);

        var tail = Align4(0x1A000 + 0x7FFF);
        Junk(tail, Length);
        return image;
    }

    private static long Align4(long value) => (value + 3) & ~3L;

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }
}
