namespace WiiSharp.Tests;

[TestClass]
public class CryptoTests
{
    [TestMethod]
    public void ClusterCipherMatchesTheReferenceAndRoundTrips()
    {
        using var cipher = new ClusterCipher(FakeDisc.TitleKey);
        var plain = FakeDisc.PlainCluster(0);
        var expected = FakeDisc.ReferenceEncrypt(plain);

        var actual = (byte[])plain.Clone();
        cipher.Encrypt(actual);
        CollectionAssert.AreEqual(expected, actual);

        cipher.Decrypt(actual);
        CollectionAssert.AreEqual(plain, actual);
    }

    [TestMethod]
    public void ClusterCipherRejectsWrongSizes()
    {
        using var cipher = new ClusterCipher(FakeDisc.TitleKey);

        Assert.ThrowsExactly<ArgumentException>(() => cipher.Encrypt(new byte[DiscFormat.ClusterSize - 1]));
        Assert.ThrowsExactly<ArgumentNullException>(() => cipher.Decrypt(null!));
    }

    [TestMethod]
    public void DiscCipherDecryptsThenEncryptsBackToTheOriginal()
    {
        var encrypted = FakeDisc.Build(encrypted: true);
        var plain = FakeDisc.Build(encrypted: false);
        var cipher = new DiscCipher(FakeDisc.CommonKey);

        var decrypted = new MemoryStream();
        var range = cipher.Decrypt(new MemoryStream(encrypted), decrypted);
        CollectionAssert.AreEqual(plain, decrypted.ToArray());
        Assert.AreEqual(new DataRange(FakeDisc.DataPartitionOffset, FakeDisc.DataStart + FakeDisc.Clusters * DiscFormat.ClusterSize), range);

        var reencrypted = new MemoryStream();
        cipher.Encrypt(new MemoryStream(decrypted.ToArray()), reencrypted);
        CollectionAssert.AreEqual(encrypted, reencrypted.ToArray());
    }

    [TestMethod]
    public void DiscCipherHonoursCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsExactly<OperationCanceledException>(() =>
            new DiscCipher(FakeDisc.CommonKey).Decrypt(new MemoryStream(FakeDisc.Build(encrypted: true)), new MemoryStream(), cts.Token));
    }

    [TestMethod]
    public void KeysRejectWrongLengthsAndCompareByValue()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new CommonKey(new byte[15]));
        Assert.ThrowsExactly<ArgumentException>(() => new TitleKey(new byte[17]));
        Assert.AreEqual(new CommonKey(new byte[16]), new CommonKey(new byte[16]));
        Assert.AreNotEqual(FakeDisc.TitleKey, new TitleKey(new byte[16]));
    }

    [TestMethod]
    public void TitleKeyDeriveUnwrapsTheTicket()
    {
        var disc = WiiDisc.Read(new MemoryStream(FakeDisc.Build(encrypted: true)));

        var key = TitleKey.Derive(FakeDisc.CommonKey, disc.DataPartitions[0].Ticket);

        Assert.AreEqual(FakeDisc.TitleKey, key);
    }
}
