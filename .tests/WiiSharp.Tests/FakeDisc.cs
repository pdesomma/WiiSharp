using System.Security.Cryptography;
using System.Text;

namespace WiiSharp.Tests;

/// <summary>
/// Builds a tiny Wii disc image: an update partition and one data partition with two clusters.
/// </summary>
internal static class FakeDisc
{
    public const long DataPartitionOffset = 0x60000;
    public const long DataStart = DataPartitionOffset + 0x20000;
    public const int Clusters = 2;
    public const int TrailerSize = 0x100;
    public const long UpdatePartitionOffset = 0x50000;
    public static readonly CommonKey CommonKey = new(Enumerable.Range(0x40, 16).Select(i => (byte)i).ToArray());
    public static readonly byte[] TitleId = { 0x00, 0x01, 0x00, 0x00, 0x52, 0x54, 0x45, 0x53 };
    public static readonly TitleKey TitleKey = new(Enumerable.Range(0x80, 16).Select(i => (byte)(i * 3)).ToArray());

    public static byte[] Build(bool encrypted)
    {
        var disc = new byte[DataStart + Clusters * DiscFormat.ClusterSize + TrailerSize];

        Encoding.ASCII.GetBytes("RTESTE").CopyTo(disc, 0);
        disc[6] = 1;
        disc[7] = 2;
        WriteUInt32(disc, 0x18, DiscFormat.WiiMagic);
        Encoding.ASCII.GetBytes("Fake Test Disc").CopyTo(disc, 0x20);

        WriteUInt32(disc, 0x40000, 2);
        WriteUInt32(disc, 0x40004, 0x40020 >> 2);
        WriteUInt32(disc, 0x40020, (uint)(UpdatePartitionOffset >> 2));
        WriteUInt32(disc, 0x40024, (uint)PartitionType.Update);
        WriteUInt32(disc, 0x40028, (uint)(DataPartitionOffset >> 2));
        WriteUInt32(disc, 0x4002C, (uint)PartitionType.Data);

        WriteTicket(disc, UpdatePartitionOffset);
        WriteTicket(disc, DataPartitionOffset);
        WriteUInt32(disc, (int)DataPartitionOffset + Ticket.Size + 0x14, 0x20000 >> 2);
        WriteUInt32(disc, (int)DataPartitionOffset + Ticket.Size + 0x18, (uint)((Clusters * DiscFormat.ClusterSize) >> 2));

        for (var c = 0; c < Clusters; c++)
        {
            var cluster = PlainCluster(c);
            if (encrypted)
                cluster = ReferenceEncrypt(cluster);
            cluster.CopyTo(disc, DataStart + c * DiscFormat.ClusterSize);
        }

        for (var i = 0; i < TrailerSize; i++)
            disc[disc.Length - TrailerSize + i] = (byte)(0xC0 + i);

        return disc;
    }

    public static byte[] PlainCluster(int index)
    {
        var cluster = new byte[DiscFormat.ClusterSize];
        for (var i = 0; i < cluster.Length; i++)
            cluster[i] = (byte)(index * 101 + i * 7);
        return cluster;
    }

    public static byte[] ReferenceEncrypt(byte[] plain)
    {
        var key = TitleKey.ToArray();
        var hashes = Cbc(key, new byte[16], plain.Take(DiscFormat.ClusterHashSize).ToArray(), encrypt: true);
        var iv = hashes.Skip(DiscFormat.ClusterIvOffset).Take(16).ToArray();
        var data = Cbc(key, iv, plain.Skip(DiscFormat.ClusterHashSize).ToArray(), encrypt: true);
        return hashes.Concat(data).ToArray();
    }

    private static byte[] Cbc(byte[] key, byte[] iv, byte[] data, bool encrypt)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = key;
        aes.IV = iv;
        using var t = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
        return t.TransformFinalBlock(data, 0, data.Length);
    }

    private static void WriteTicket(byte[] disc, long offset)
    {
        var iv = new byte[16];
        TitleId.CopyTo(iv, 0);
        var wrapped = Cbc(CommonKey.ToArray(), iv, TitleKey.ToArray(), encrypt: true);
        wrapped.CopyTo(disc, offset + Ticket.EncryptedTitleKeyOffset);
        TitleId.CopyTo(disc, offset + Ticket.TitleIdOffset);
        disc[offset + Ticket.CommonKeyIndexOffset] = 0;
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }
}
