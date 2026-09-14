using System.Text;

namespace WiiSharp.Tests;

[TestClass]
public class SignedBlobTests
{
    private static readonly byte[] TitleId = { 0x00, 0x01, 0x00, 0x00, 0x47, 0x41, 0x4C, 0x45 };

    [TestMethod]
    public void TicketBuild_SetsIdsMaskAndFakeSignature()
    {
        var key = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();

        var ticket = Ticket.Build(TitleId, key);
        var bytes = ticket.ToBytes();

        Assert.AreEqual(Ticket.Size, bytes.Length);
        CollectionAssert.AreEqual(TitleId, ticket.TitleId);
        CollectionAssert.AreEqual(key, ticket.EncryptedTitleKey);
        Assert.AreEqual(0, ticket.CommonKeyIndex);
        Assert.AreNotEqual(0UL, ticket.TicketId);
        Assert.AreEqual(Signature.Rsa2048Type, (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]));
        Assert.AreEqual(Ticket.Issuer, Signature.ReadIssuer(bytes));
        Assert.IsTrue(bytes.Skip(Ticket.ContentAccessOffset).Take(Ticket.ContentAccessSize).All(b => b == 0xFF));
        Assert.IsTrue(bytes.Skip(4).Take(256).All(b => b == 0), "signature left blank");
        Assert.IsTrue(Signature.IsFakeSigned(bytes));
    }

    [TestMethod]
    public void TicketBuild_BadArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Ticket.Build(null!, new byte[16]));
        Assert.ThrowsExactly<ArgumentException>(() => Ticket.Build(new byte[7], new byte[16]));
        Assert.ThrowsExactly<ArgumentNullException>(() => Ticket.Build(TitleId, null!));
        Assert.ThrowsExactly<ArgumentException>(() => Ticket.Build(TitleId, new byte[15]));
    }

    [TestMethod]
    public void TmdBuild_RoundTripsThroughParse()
    {
        var hash = Enumerable.Range(0, 20).Select(i => (byte)(i * 3)).ToArray();
        var region = RegionSettings.Preset(DiscRegion.Europe);

        var tmd = Tmd.Build(TitleId, WiiDiscBuilder.DefaultIosVersion, 0x3031, region, new TmdContent(0, 0, Tmd.DiscContentType, 0x1234000, hash));
        var parsed = Tmd.Parse(tmd.ToBytes());

        Assert.AreEqual(Tmd.ContentsOffset + Tmd.ContentRecordSize, parsed.Size);
        CollectionAssert.AreEqual(TitleId, parsed.TitleId);
        Assert.AreEqual(WiiDiscBuilder.DefaultIosVersion, parsed.IosVersion);
        Assert.AreEqual((ushort)0x3031, parsed.GroupId);
        Assert.AreEqual(DiscRegion.Europe, parsed.Region);
        Assert.AreEqual(Tmd.DiscTitleType, parsed.TitleType);
        Assert.AreEqual(Tmd.Issuer, Signature.ReadIssuer(parsed.ToBytes()));
        CollectionAssert.AreEqual(region.Ratings, parsed.ToBytes().Skip(0x19E).Take(16).ToArray());
        Assert.AreEqual(1, parsed.Contents.Count);
        Assert.AreEqual(Tmd.DiscContentType, parsed.Contents[0].Type);
        Assert.AreEqual(0x1234000UL, parsed.Contents[0].Size);
        CollectionAssert.AreEqual(hash, parsed.Contents[0].Hash);
        Assert.IsTrue(Signature.IsFakeSigned(parsed.ToBytes()));
    }

    [TestMethod]
    public void TmdParse_LengthMismatch_ThrowsInvalidDataException()
    {
        var bytes = new byte[Tmd.ContentsOffset + Tmd.ContentRecordSize];
        bytes[0x1DF] = 2;

        Assert.ThrowsExactly<InvalidDataException>(() => Tmd.Parse(bytes));
        Assert.ThrowsExactly<ArgumentException>(() => Tmd.Parse(new byte[10]));
        Assert.ThrowsExactly<ArgumentNullException>(() => Tmd.Parse(null!));
    }

    [TestMethod]
    public void FakeSign_LeavesSignatureAndBodyOutsideFillerUntouched()
    {
        var blob = new byte[0x400];
        for (var i = 0; i < blob.Length; i++)
            blob[i] = (byte)(i * 13);
        Array.Clear(blob, 4, 256);
        var before = (byte[])blob.Clone();

        Signature.FakeSign(blob, 0x3F0);

        Assert.IsTrue(Signature.IsFakeSigned(blob));
        Assert.IsTrue(before.Take(0x3F0).SequenceEqual(blob.Take(0x3F0)));
        Assert.IsTrue(before.Skip(0x3F2).SequenceEqual(blob.Skip(0x3F2)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Signature.FakeSign(blob, 0x10));
        Assert.ThrowsExactly<ArgumentNullException>(() => Signature.FakeSign(null!, 0x3F0));
    }

    [TestMethod]
    public void ReadIssuer_StopsAtNul()
    {
        var blob = new byte[0x200];
        Encoding.ASCII.GetBytes("Root-CA00000001-XS00000003").CopyTo(blob, Signature.BodyOffset);

        Assert.AreEqual("Root-CA00000001-XS00000003", Signature.ReadIssuer(blob));
    }
}
