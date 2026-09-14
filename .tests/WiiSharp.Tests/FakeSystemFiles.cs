using System.Text;

namespace WiiSharp.Tests;

/// <summary>
/// Synthetic boot.bin, bi2.bin, apploader and certificate chain for disc builds.
/// </summary>
internal static class FakeSystemFiles
{
    public const int ApploaderCode = 0x100;
    public const int ApploaderTrailer = 0x40;
    public const uint UserPosition = 0x817E5A00;

    public static byte[] ApploaderBytes()
    {
        var bytes = new byte[Apploader.HeaderSize + ApploaderCode + ApploaderTrailer];
        Encoding.ASCII.GetBytes("2026/01/01").CopyTo(bytes, 0);
        Write(bytes, 0x10, 0x81200000);
        Write(bytes, 0x14, ApploaderCode);
        Write(bytes, 0x18, ApploaderTrailer);
        for (var i = Apploader.HeaderSize; i < bytes.Length; i++)
            bytes[i] = (byte)(0xA0 + i);
        return bytes;
    }

    public static byte[] Bi2()
    {
        var bi2 = new byte[DiscFormat.Bi2Size];
        Write(bi2, 0x1C, 1);
        Write(bi2, 0x20, 1);
        Write(bi2, 0x24, 5);
        Write(bi2, 0x2C, 0x04000000);
        Write(bi2, 0x30, 0x7ED40000);
        return bi2;
    }

    public static byte[] Boot()
    {
        var boot = new byte[DiscFormat.BootSize];
        Encoding.ASCII.GetBytes("RBASE1").CopyTo(boot, 0);
        Write(boot, 0x18, DiscFormat.WiiMagic);
        Encoding.ASCII.GetBytes("Base Game").CopyTo(boot, 0x20);
        Write(boot, 0x420, 0x10000 >> 2);
        Write(boot, 0x424, 0x20000 >> 2);
        Write(boot, 0x428, 0x1000 >> 2);
        Write(boot, 0x42C, 0x1000 >> 2);
        Write(boot, 0x430, UserPosition);
        Write(boot, 0x434, 0x1A600);
        return boot;
    }

    public static byte[] CertificateChain() => Enumerable.Range(0, 0xA00).Select(i => (byte)(i ^ 0x5A)).ToArray();

    public static PartitionSystemFiles Create() =>
        new(Boot(), Bi2(), Apploader.Parse(ApploaderBytes()), CertificateChain());

    private static void Write(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }
}
