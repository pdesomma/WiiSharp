namespace WiiSharp.Tests;

[TestClass]
public class DolFileAndGeckoTests
{
    [TestMethod]
    public void DolFile_ParseThenToBytes_RoundTripsSectionsAndHeaderFields()
    {
        var original = Dol();

        var dol = DolFile.Parse(original);
        var bytes = dol.ToBytes();

        var header = DolHeader.Parse(bytes);
        Assert.AreEqual(0x80004000u, header.EntryPoint);
        Assert.AreEqual(0x80100000u, header.BssAddress);
        Assert.AreEqual(0x1234u, header.BssSize);
        Assert.AreEqual(1, header.TextSections.Count);
        Assert.AreEqual(1, header.DataSections.Count);
        CollectionAssert.AreEqual(Enumerable.Range(0, 0x200).Select(i => (byte)i).ToArray(), dol.Read(0x80004000, 0x200));
        CollectionAssert.AreEqual(Enumerable.Range(0, 0x40).Select(i => (byte)(i ^ 0x55)).ToArray(), dol.Read(0x80010000, 0x40));
        Assert.AreEqual(2, dol.Sections.Count());
    }

    [TestMethod]
    public void DolFile_AddSection_TakesTheFirstFreeSlotAndSerialises()
    {
        var dol = DolFile.Parse(Dol());
        var handler = Enumerable.Range(0, 0x30).Select(i => (byte)(0xA0 + i)).ToArray();

        var slot = dol.AddSection(0x80001800, handler, text: true);
        var dataSlot = dol.AddSection(0x80020000, new byte[] { 1, 2, 3 }, text: false);

        Assert.AreEqual(1, slot);
        Assert.AreEqual(DolHeader.TextSectionCount + 1, dataSlot);
        var reparsed = DolFile.Parse(dol.ToBytes());
        CollectionAssert.AreEqual(handler, reparsed.Read(0x80001800, handler.Length));
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, reparsed.Read(0x80020000, 3));
        Assert.AreEqual(0x80004000u, DolHeader.Parse(dol.ToBytes()).EntryPoint);
    }

    [TestMethod]
    public void DolFile_AddSection_RejectsOverlapsAndFullTables()
    {
        var dol = DolFile.Parse(Dol());

        Assert.ThrowsExactly<InvalidOperationException>(() => dol.AddSection(0x80004100, new byte[4], text: true), "inside the text section");
        Assert.ThrowsExactly<InvalidOperationException>(() => dol.AddSection(0x80003FF0, new byte[0x20], text: false), "straddling its start");
        for (var i = 1; i < DolHeader.TextSectionCount; i++)
            dol.AddSection((uint)(0x81000000 + i * 0x100), new byte[8], text: true);
        Assert.ThrowsExactly<InvalidOperationException>(() => dol.AddSection(0x81F00000, new byte[8], text: true), "no slot left");
        Assert.ThrowsExactly<ArgumentNullException>(() => dol.AddSection(0, null!, text: true));
        Assert.ThrowsExactly<ArgumentException>(() => dol.AddSection(0x81F00000, Array.Empty<byte>(), text: true));
    }

    [TestMethod]
    public void DolFile_FindAndWrite_WorkByAddress()
    {
        var dol = DolFile.Parse(Dol());

        Assert.AreEqual(0x80004010u, dol.Find(new byte[] { 0x10, 0x11, 0x12, 0x13 }));
        Assert.AreEqual(0x80010004u, dol.Find(new byte[] { 0x51, 0x50, 0x53, 0x52 }, alignment: 1));
        Assert.IsNull(dol.Find(new byte[] { 0x11, 0x12, 0x13, 0x14 }), "misaligned by one is not found at word alignment");
        Assert.AreEqual(0x80004011u, dol.Find(new byte[] { 0x11, 0x12, 0x13, 0x14 }, alignment: 1));
        Assert.AreEqual(0x80004110u, dol.Find(new byte[] { 0x10, 0x11, 0x12, 0x13 }, start: 0x80004014), "the byte ramp repeats every 0x100");
        Assert.IsNull(dol.Find(new byte[] { 9, 9, 9, 9, 9 }));

        dol.Write(0x80004010, new byte[] { 0xDE, 0xAD });
        CollectionAssert.AreEqual(new byte[] { 0xDE, 0xAD, 0x12, 0x13 }, dol.Read(0x80004010, 4));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => dol.Read(0x800041FE, 4), "runs past the section");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => dol.Write(0x80000000, new byte[4]));
        Assert.ThrowsExactly<ArgumentNullException>(() => dol.Write(0x80004000, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => dol.Find(null!));
        Assert.ThrowsExactly<ArgumentException>(() => dol.Find(Array.Empty<byte>()));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => dol.Find(new byte[1], alignment: 0));
    }

    [TestMethod]
    public void DolFile_Parse_RejectsBadInput()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => DolFile.Parse(null!));
        Assert.ThrowsExactly<ArgumentException>(() => DolFile.Parse(new byte[0x10]));
        var truncated = Dol().Take(0x200).ToArray();
        Assert.ThrowsExactly<InvalidDataException>(() => DolFile.Parse(truncated));
    }

    [TestMethod]
    public void GeckoCodes_BuildThenParse_RoundTrips()
    {
        var lines = new[] { new GeckoCodeLine(0x04123456, 0x60000000), new GeckoCodeLine(0xC2222222, 0x00000002), new GeckoCodeLine(0x38600001, 0x4E800020), new GeckoCodeLine(0x60000000, 0x00000000) };

        var bytes = GeckoCodes.Build(lines);

        CollectionAssert.AreEqual(GeckoCodes.Magic, bytes.Take(8).ToArray());
        CollectionAssert.AreEqual(GeckoCodes.Terminator, bytes.Skip(bytes.Length - 8).ToArray());
        Assert.AreEqual(8 + 4 * 8 + 8, bytes.Length);
        CollectionAssert.AreEqual(lines, GeckoCodes.Parse(bytes).ToArray());
    }

    [TestMethod]
    public void GeckoCodes_ParseText_ReadsOcarinaAndDolphinLayouts()
    {
        const string ocarina = "RMCE01\r\nMario Kart Wii\r\n\r\nInfinite Coins [someone]\r\n04123456 00000063\r\n\r\nASM thing\r\nC2222222 00000002\r\n38600001 4E800020\r\n60000000 00000000\r\nnot a code line\r\n";
        const string dolphin = "[Core]\r\nEmulationSpeed = 1\r\n[ActionReplay]\r\n$AR\r\n0A123456 00000001\r\n[Gecko]\r\n$Widescreen [gamemasterplc]\r\n*works in 16:9\r\n048E60D0 3FE38E39\r\n[Gecko_Enabled]\r\n$Widescreen\r\n";

        var fromOcarina = GeckoCodes.ParseText(ocarina);
        var fromDolphin = GeckoCodes.ParseText(dolphin);

        CollectionAssert.AreEqual(new[] { new GeckoCodeLine(0x04123456, 0x63), new GeckoCodeLine(0xC2222222, 2), new GeckoCodeLine(0x38600001, 0x4E800020), new GeckoCodeLine(0x60000000, 0) }, fromOcarina.ToArray());
        CollectionAssert.AreEqual(new[] { new GeckoCodeLine(0x048E60D0, 0x3FE38E39) }, fromDolphin.ToArray(), "only the [Gecko] section counts");
    }

    [TestMethod]
    public void GeckoCodes_Load_TellsGctFromText()
    {
        var root = Path.Combine(Path.GetTempPath(), "WiiSharp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var gct = Path.Combine(root, "codes.gct");
            File.WriteAllBytes(gct, GeckoCodes.Build(new[] { new GeckoCodeLine(1, 2) }));
            var txt = Path.Combine(root, "codes.txt");
            File.WriteAllText(txt, "RMCE01\nGame\n\nName\n00000003 00000004\n");

            CollectionAssert.AreEqual(new[] { new GeckoCodeLine(1, 2) }, GeckoCodes.Load(gct).ToArray());
            CollectionAssert.AreEqual(new[] { new GeckoCodeLine(3, 4) }, GeckoCodes.Load(txt).ToArray());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void GeckoCodes_InvalidInput_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => GeckoCodes.Build(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => GeckoCodes.Parse(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => GeckoCodes.ParseText(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => GeckoCodes.Load(null!));
        Assert.ThrowsExactly<InvalidDataException>(() => GeckoCodes.Parse(new byte[16]), "no magic");
        Assert.ThrowsExactly<InvalidDataException>(() => GeckoCodes.Parse(GeckoCodes.Magic.Concat(new byte[3]).ToArray()), "ragged");
        Assert.ThrowsExactly<InvalidDataException>(() => GeckoCodes.ParseText("just a name\n"));
        Assert.AreEqual(0, GeckoCodes.Parse(GeckoCodes.Build(Array.Empty<GeckoCodeLine>())).Count);
    }

    /// <summary>
    /// A DOL with one text section at 0x80004000 (0x200 bytes counting up) and one data section at 0x80010000 (0x40 bytes).
    /// </summary>
    private static byte[] Dol()
    {
        var text = Enumerable.Range(0, 0x200).Select(i => (byte)i).ToArray();
        var data = Enumerable.Range(0, 0x40).Select(i => (byte)(i ^ 0x55)).ToArray();
        var bytes = new byte[DolHeader.Size + text.Length + data.Length];
        Write(bytes, 0x00, DolHeader.Size);
        Write(bytes, 0x48, 0x80004000);
        Write(bytes, 0x90, (uint)text.Length);
        Write(bytes, 7 * 4, (uint)(DolHeader.Size + text.Length));
        Write(bytes, 0x48 + 7 * 4, 0x80010000);
        Write(bytes, 0x90 + 7 * 4, (uint)data.Length);
        Write(bytes, 0xD8, 0x80100000);
        Write(bytes, 0xDC, 0x1234);
        Write(bytes, 0xE0, 0x80004000);
        text.CopyTo(bytes, DolHeader.Size);
        data.CopyTo(bytes, DolHeader.Size + text.Length);
        return bytes;
    }

    private static void Write(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }
}
