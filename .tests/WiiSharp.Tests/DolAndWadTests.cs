namespace WiiSharp.Tests;

[TestClass]
public class DolAndWadTests
{
    private static readonly byte[] TitleId = { 0x00, 0x01, 0x00, 0x01, 0x57, 0x41, 0x4C, 0x50 };

    [TestMethod]
    public void DolHeaderParse_ListsSectionsWithBytesAndComputesLength()
    {
        var header = new byte[DolHeader.Size];
        Write(header, 0x00, 0x100);
        Write(header, 0x48, 0x80004000);
        Write(header, 0x90, 0x40);
        Write(header, 0x1C, 0x1000);
        Write(header, 0x64, 0x80100000);
        Write(header, 0xAC, 0x10);
        Write(header, 0xD8, 0x80200000);
        Write(header, 0xDC, 0x8000);
        Write(header, 0xE0, 0x80004020);

        var dol = DolHeader.Parse(header);

        Assert.AreEqual(1, dol.TextSections.Count);
        Assert.AreEqual(new DolSection(0x100, 0x80004000, 0x40), dol.TextSections[0]);
        Assert.AreEqual(1, dol.DataSections.Count);
        Assert.AreEqual(new DolSection(0x1000, 0x80100000, 0x10), dol.DataSections[0]);
        Assert.AreEqual(0x1010, dol.Length);
        Assert.AreEqual(0x80200000u, dol.BssAddress);
        Assert.AreEqual(0x8000u, dol.BssSize);
        Assert.AreEqual(0x80004020u, dol.EntryPoint);
    }

    [TestMethod]
    public void DolHeaderParse_BadInput_Throws()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => DolHeader.Parse(new byte[DolHeader.Size]));
        Assert.ThrowsExactly<ArgumentException>(() => DolHeader.Parse(new byte[0x10]));
        Assert.ThrowsExactly<ArgumentNullException>(() => DolHeader.Parse(null!));
    }

    [TestMethod]
    public void WadRead_FindsTicketAndTmdBehindPaddedSections()
    {
        var wad = new MemoryStream(BuildWad(certificateChainLength: 0xA00));

        var file = WadFile.Read(wad);

        Assert.AreEqual(WadHeader.InstallableType, file.Header.Type);
        Assert.AreEqual(0xA40, file.Header.TicketOffset);
        Assert.AreEqual(0xD00, file.Header.TmdOffset, "ticket 0x2A4 rounded up to 0x40");
        Assert.AreEqual(0xF40, file.Header.DataOffset);
        CollectionAssert.AreEqual(TitleId, file.TitleId);
        CollectionAssert.AreEqual(TitleId, file.Tmd.TitleId);
        Assert.AreEqual(1, file.Tmd.Contents.Count);
    }

    [TestMethod]
    public void WadRead_OddCertificateLength_StillAlignsSections()
    {
        var file = WadFile.Read(new MemoryStream(BuildWad(certificateChainLength: 0x9F1)));

        Assert.AreEqual(0xA40, file.Header.TicketOffset);
        CollectionAssert.AreEqual(TitleId, file.TitleId);
    }

    [TestMethod]
    public void WadRead_NotAWadOrNull_Throws()
    {
        var wad = BuildWad(0xA00);
        wad[3] = 0x21;

        Assert.ThrowsExactly<InvalidDataException>(() => WadFile.Read(new MemoryStream(wad)));
        Assert.ThrowsExactly<ArgumentNullException>(() => WadFile.Read(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => WadFile.Open(null!));
        Assert.ThrowsExactly<ArgumentException>(() => WadHeader.Parse(new byte[4]));
    }

    private static byte[] BuildWad(int certificateChainLength)
    {
        var ticket = Ticket.Build(TitleId, new byte[16]).ToBytes();
        var tmd = Tmd.Build(TitleId, 0x0000000100000035, 0x3031, RegionSettings.Preset(DiscRegion.UnitedStates), new TmdContent(0, 0, 1, 0x40, new byte[20])).ToBytes();
        var header = new byte[WadHeader.Size];
        Write(header, 0, WadHeader.Size);
        header[4] = 0x49;
        header[5] = 0x73;
        Write(header, 8, (uint)certificateChainLength);
        Write(header, 0x10, (uint)ticket.Length);
        Write(header, 0x14, (uint)tmd.Length);
        Write(header, 0x18, 0x40);

        var wad = new MemoryStream();
        Put(wad, header);
        Put(wad, Enumerable.Range(0, certificateChainLength).Select(i => (byte)i).ToArray());
        Put(wad, ticket);
        Put(wad, tmd);
        Put(wad, new byte[0x40]);
        return wad.ToArray();
    }

    private static void Put(MemoryStream wad, byte[] section)
    {
        wad.Write(section, 0, section.Length);
        var padding = (WadHeader.Alignment - (int)(wad.Length % WadHeader.Alignment)) % WadHeader.Alignment;
        wad.Write(new byte[padding], 0, padding);
    }

    private static void Write(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }
}
