namespace WiiSharp;

/// <summary>
/// The pieces of a partition a new disc can reuse: boot.bin, bi2.bin, the apploader and the certificate chain.
/// </summary>
public sealed class PartitionSystemFiles
{
    private readonly byte[] _bi2;
    private readonly byte[] _boot;
    private readonly byte[] _certificateChain;

    /// <summary>
    /// Creates a new instance of the <see cref="PartitionSystemFiles"/> class.
    /// </summary>
    /// <param name="boot">boot.bin, <see cref="DiscFormat.BootSize"/> bytes.</param>
    /// <param name="bi2">bi2.bin, <see cref="DiscFormat.Bi2Size"/> bytes.</param>
    /// <param name="apploader">Apploader.</param>
    /// <param name="certificateChain">Certificate chain from the partition header area.</param>
    public PartitionSystemFiles(byte[] boot, byte[] bi2, Apploader apploader, byte[] certificateChain)
    {
        if (boot is null)
            throw new ArgumentNullException(nameof(boot));
        if (boot.Length != DiscFormat.BootSize)
            throw new ArgumentException($"boot.bin is {DiscFormat.BootSize} bytes.", nameof(boot));
        if (bi2 is null)
            throw new ArgumentNullException(nameof(bi2));
        if (bi2.Length != DiscFormat.Bi2Size)
            throw new ArgumentException($"bi2.bin is {DiscFormat.Bi2Size} bytes.", nameof(bi2));
        if (certificateChain is null)
            throw new ArgumentNullException(nameof(certificateChain));
        if (certificateChain.Length == 0)
            throw new ArgumentException("Certificate chain must not be empty.", nameof(certificateChain));

        _boot = (byte[])boot.Clone();
        _bi2 = (byte[])bi2.Clone();
        Apploader = apploader ?? throw new ArgumentNullException(nameof(apploader));
        _certificateChain = (byte[])certificateChain.Clone();
    }

    /// <summary>
    /// Apploader.
    /// </summary>
    public Apploader Apploader { get; }
    /// <summary>
    /// Copy of bi2.bin.
    /// </summary>
    public byte[] Bi2 => (byte[])_bi2.Clone();
    /// <summary>
    /// Copy of boot.bin.
    /// </summary>
    public byte[] Boot => (byte[])_boot.Clone();
    /// <summary>
    /// Copy of the certificate chain.
    /// </summary>
    public byte[] CertificateChain => (byte[])_certificateChain.Clone();

    /// <summary>
    /// Reads the files from a partition whose clusters are plaintext.
    /// </summary>
    /// <param name="disc">Seekable plaintext disc image.</param>
    /// <param name="partition">Partition to read.</param>
    public static PartitionSystemFiles Read(Stream disc, Partition partition) => Read(disc, partition, hashed: true);

    /// <summary>
    /// Reads the files from a partition whose clusters are plaintext.
    /// </summary>
    /// <param name="disc">Seekable plaintext disc image.</param>
    /// <param name="partition">Partition to read.</param>
    /// <param name="hashed">False when the payload is stored bare, without hash blocks, as NKit writes it.</param>
    public static PartitionSystemFiles Read(Stream disc, Partition partition, bool hashed)
    {
        if (disc is null)
            throw new ArgumentNullException(nameof(disc));
        if (partition is null)
            throw new ArgumentNullException(nameof(partition));
        if (partition.Header.CertificateChainSize == 0)
            throw new InvalidDataException("Partition has no certificate chain.");

        var chain = disc.ReadExactlyAt(partition.Offset + partition.Header.CertificateChainOffset, checked((int)partition.Header.CertificateChainSize));
        var data = new PartitionDataStream(disc, partition, hashed);
        var boot = data.ReadExactlyAt(0, DiscFormat.BootSize);
        var bi2 = data.ReadExactly(DiscFormat.Bi2Size);
        return new PartitionSystemFiles(boot, bi2, Apploader.Read(data), chain);
    }
}
