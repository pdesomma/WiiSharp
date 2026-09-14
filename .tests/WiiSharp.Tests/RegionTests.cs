namespace WiiSharp.Tests;

[TestClass]
public class RegionTests
{
    [TestMethod]
    public void ParseReadsBackWhatToBytesWrote()
    {
        var ratings = Enumerable.Range(1, 16).Select(i => (byte)i).ToArray();
        var original = new RegionSettings(DiscRegion.Korea, ratings);

        var parsed = RegionSettings.Parse(original.ToBytes());

        Assert.AreEqual(DiscRegion.Korea, parsed.Region);
        CollectionAssert.AreEqual(ratings, parsed.Ratings);
    }

    [TestMethod]
    public void PresetsMatchTheInjectorsBytes()
    {
        Assert.AreEqual(0x01, RegionSettings.Preset(DiscRegion.UnitedStates).ToBytes()[3]);
        CollectionAssert.AreEqual(
            new byte[] { 0x80, 0x06, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80 },
            RegionSettings.Preset(DiscRegion.UnitedStates).Ratings);

        Assert.AreEqual(0x00, RegionSettings.Preset(DiscRegion.Japan).ToBytes()[3]);
        CollectionAssert.AreEqual(
            new byte[] { 0x00, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80 },
            RegionSettings.Preset(DiscRegion.Japan).Ratings);

        Assert.AreEqual(0x02, RegionSettings.Preset(DiscRegion.Europe).ToBytes()[3]);
        CollectionAssert.AreEqual(
            new byte[] { 0x80, 0x80, 0x80, 0x00, 0x03, 0x03, 0x04, 0x03, 0x00, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80 },
            RegionSettings.Preset(DiscRegion.Europe).Ratings);
    }

    [TestMethod]
    public void PresetRejectsKorea()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RegionSettings.Preset(DiscRegion.Korea));
    }

    [TestMethod]
    public void RegionAreaWritesAt4E000AndReadsBack()
    {
        var disc = new MemoryStream(FakeDisc.Build(encrypted: true));
        var europe = RegionSettings.Preset(DiscRegion.Europe);

        RegionArea.Write(disc, europe);
        var bytes = disc.ToArray();
        var read = RegionArea.Read(new MemoryStream(bytes));

        Assert.AreEqual(0x02, bytes[0x4E003]);
        Assert.AreEqual(0x03, bytes[0x4E014]);
        Assert.AreEqual(DiscRegion.Europe, read.Region);
        CollectionAssert.AreEqual(europe.Ratings, read.Ratings);
        CollectionAssert.AreEqual(new byte[12], bytes.Skip(0x4E004).Take(12).ToArray());
    }

    [TestMethod]
    public void RejectsWrongRatingLength()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new RegionSettings(DiscRegion.Japan, new byte[15]));
        Assert.ThrowsExactly<ArgumentException>(() => RegionSettings.Parse(new byte[0x10]));
    }
}
