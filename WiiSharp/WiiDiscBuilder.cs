using System.Security.Cryptography;
using System.Text;

namespace WiiSharp;

/// <summary>
/// Writes a single-partition Wii disc image whose clusters are plaintext but fully hashed, ticket and TMD fakesigned.
/// </summary>
public sealed class WiiDiscBuilder
{
    /// <summary>
    /// IOS35, what retail discs and the vWii disc framework expect.
    /// </summary>
    public const ulong DefaultIosVersion = 0x0000000100000023;
    /// <summary>
    /// Alignment of main.dol and the FST.
    /// </summary>
    public const int SystemAlignment = 0x20;
    /// <summary>
    /// Offset of the TMD within the partition.
    /// </summary>
    public const int TmdOffset = 0x2C0;
    /// <summary>
    /// High word of disc title IDs.
    /// </summary>
    public const uint DiscTitleHigh = 0x00010000;

    private readonly byte[] _mainDol;

    /// <summary>
    /// Creates a new instance of the <see cref="WiiDiscBuilder"/> class.
    /// </summary>
    /// <param name="gameId">Six-character game ID.</param>
    /// <param name="title">Title, at most 64 ASCII characters.</param>
    /// <param name="system">boot.bin and bi2.bin templates, apploader and certificate chain.</param>
    /// <param name="mainDol">Executable the apploader boots.</param>
    public WiiDiscBuilder(string gameId, string title, PartitionSystemFiles system, byte[] mainDol)
    {
        if (gameId is null)
            throw new ArgumentNullException(nameof(gameId));
        if (gameId.Length != 6 || gameId.Any(c => c > 0x7F))
            throw new ArgumentException("Game ID is six ASCII characters.", nameof(gameId));
        if (title is null)
            throw new ArgumentNullException(nameof(title));
        if (title.Length > 0x40 || title.Any(c => c > 0x7F))
            throw new ArgumentException("Title is at most 64 ASCII characters.", nameof(title));
        if (mainDol is null)
            throw new ArgumentNullException(nameof(mainDol));
        if (mainDol.Length == 0)
            throw new ArgumentException("main.dol must not be empty.", nameof(mainDol));

        GameId = gameId;
        Title = title;
        System = system ?? throw new ArgumentNullException(nameof(system));
        _mainDol = (byte[])mainDol.Clone();
    }

    /// <summary>
    /// Zero-based disc number.
    /// </summary>
    public byte DiscNumber { get; init; }
    /// <summary>
    /// Sixteen bytes stored as the encrypted title key; unused while the data stays plaintext.
    /// </summary>
    public byte[] EncryptedTitleKey { get; init; } = new byte[DiscFormat.KeySize];
    /// <summary>
    /// Alignment of every file; a hash-group boundary by default, as wit lays discs out.
    /// </summary>
    public int FileAlignment { get; init; } = DiscFormat.GroupDataSize;
    /// <summary>
    /// Files to place after the FST, in order.
    /// </summary>
    public IList<DiscFile> Files { get; } = new List<DiscFile>();
    /// <summary>
    /// Game ID.
    /// </summary>
    public string GameId { get; }
    /// <summary>
    /// System version written to the TMD.
    /// </summary>
    public ulong IosVersion { get; init; } = DefaultIosVersion;
    /// <summary>
    /// Where the partition starts.
    /// </summary>
    public long PartitionOffset { get; init; } = DiscFormat.RetailDataPartitionOffset;
    /// <summary>
    /// Region area and TMD region.
    /// </summary>
    public RegionSettings Region { get; init; } = RegionSettings.Preset(DiscRegion.UnitedStates);
    /// <summary>
    /// Reused partition pieces.
    /// </summary>
    public PartitionSystemFiles System { get; }
    /// <summary>
    /// Disc title.
    /// </summary>
    public string Title { get; }
    /// <summary>
    /// Disc revision.
    /// </summary>
    public byte Version { get; init; }

    /// <summary>
    /// Writes the image from offset 0.
    /// </summary>
    /// <param name="output">Seekable destination.</param>
    /// <param name="cancellationToken">Cancels between hash groups.</param>
    /// <exception cref="InvalidDataException">The files do not fit a single-layer disc.</exception>
    public WiiDiscBuildResult Build(Stream output, CancellationToken cancellationToken = default)
    {
        if (output is null)
            throw new ArgumentNullException(nameof(output));
        if (!output.CanSeek || !output.CanWrite)
            throw new ArgumentException("Output must be writable and seekable.", nameof(output));
        if (FileAlignment <= 0 || FileAlignment % 4 != 0)
            throw new InvalidOperationException("File alignment must be a positive multiple of 4.");
        if (PartitionOffset <= DiscFormat.DiscMagicOffset || PartitionOffset % 4 != 0)
            throw new InvalidOperationException("Partition offset must follow the disc header and be a multiple of 4.");

        var segments = new List<Segment>();
        var apploader = System.Apploader.ToBytes();
        segments.Add(new Segment(DiscFormat.BootSize, System.Bi2));
        segments.Add(new Segment(DiscFormat.ApploaderOffset, apploader));
        var dolOffset = Align(DiscFormat.ApploaderOffset + apploader.Length, SystemAlignment);
        segments.Add(new Segment(dolOffset, _mainDol));
        var fstOffset = Align(dolOffset + _mainDol.Length, SystemAlignment);
        var fstSize = Align(Fst.Build(Files.Select(f => new FstFile(f.Path, 0, f.Length)).ToArray()).Length, 4);

        var placed = new List<FstFile>();
        var next = Align(fstOffset + fstSize, FileAlignment);
        foreach (var file in Files)
        {
            placed.Add(new FstFile(file.Path, next, file.Length));
            segments.Add(new Segment(next, file.Content));
            next = Align(next + file.Length, FileAlignment);
        }
        var fst = Fst.Build(placed);
        Array.Resize(ref fst, (int)Align(fst.Length, 4));
        segments.Add(new Segment(fstOffset, fst));
        segments.Add(new Segment(0, BuildBoot(dolOffset, fstOffset, fst.Length)));

        var dataLength = Align(placed.Count == 0 ? fstOffset + fst.Length : placed[placed.Count - 1].Offset + placed[placed.Count - 1].Length, DiscFormat.GroupDataSize);
        var groups = dataLength / DiscFormat.GroupDataSize;
        var clusterBytes = groups * DiscFormat.GroupClusters * (long)DiscFormat.ClusterSize;
        var dataStart = PartitionOffset + DiscFormat.PartitionDataOffset;
        if (dataStart + clusterBytes > DiscFormat.SingleLayerSize)
            throw new InvalidDataException("Files do not fit a single-layer disc.");

        WriteDiscHeader(output);
        var h3 = new byte[DiscFormat.H3Size];
        WriteData(output, dataStart, segments, groups, h3, cancellationToken);

        using var sha1 = SHA1.Create();
        var titleId = new byte[8];
        BigEndian.WriteUInt32(titleId, 0, DiscTitleHigh);
        Encoding.ASCII.GetBytes(GameId, 0, 4, titleId, 4);
        var ticket = Ticket.Build(titleId, EncryptedTitleKey);
        var tmd = Tmd.Build(titleId, IosVersion, BigEndian.ReadUInt16(Encoding.ASCII.GetBytes(GameId), 4), Region,
            new TmdContent(0, 0, Tmd.DiscContentType, (ulong)clusterBytes, sha1.ComputeHash(h3)));
        WritePartitionHead(output, ticket, tmd, h3, clusterBytes);

        var length = dataStart + clusterBytes;
        output.SetLength(length);
        return new WiiDiscBuildResult(new DataRange(PartitionOffset, length), length, ticket, tmd);
    }

    private static long Align(long value, long alignment) => (value + alignment - 1) / alignment * alignment;

    private static void Fill(byte[] buffer, long start, IReadOnlyList<Segment> segments)
    {
        Array.Clear(buffer, 0, buffer.Length);
        var end = start + buffer.Length;
        foreach (var segment in segments)
        {
            var from = Math.Max(start, segment.Offset);
            var to = Math.Min(end, segment.Offset + segment.Length);
            if (from >= to)
                continue;
            segment.Read(from - segment.Offset, buffer, (int)(from - start), (int)(to - from));
        }
    }

    private static void WriteAt(Stream output, long position, byte[] bytes)
    {
        output.Position = position;
        output.Write(bytes, 0, bytes.Length);
    }

    private byte[] BuildBoot(long dolOffset, long fstOffset, int fstSize)
    {
        var boot = System.Boot;
        Array.Clear(boot, 0, 0x60);
        Encoding.ASCII.GetBytes(GameId).CopyTo(boot, 0);
        boot[6] = DiscNumber;
        boot[7] = Version;
        BigEndian.WriteUInt32(boot, 0x18, DiscFormat.WiiMagic);
        Encoding.ASCII.GetBytes(Title).CopyTo(boot, 0x20);
        BigEndian.WriteUInt32(boot, 0x420, (uint)(dolOffset >> 2));
        BigEndian.WriteUInt32(boot, 0x424, (uint)(fstOffset >> 2));
        BigEndian.WriteUInt32(boot, 0x428, (uint)(fstSize >> 2));
        BigEndian.WriteUInt32(boot, 0x42C, (uint)(fstSize >> 2));
        return boot;
    }

    private void WriteData(Stream output, long dataStart, IReadOnlyList<Segment> segments, long groups, byte[] h3, CancellationToken cancellationToken)
    {
        var data = new byte[DiscFormat.GroupDataSize];
        var clusters = new byte[DiscFormat.GroupClusters * DiscFormat.ClusterSize];
        output.Position = dataStart;
        for (long g = 0; g < groups; g++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Fill(data, g * DiscFormat.GroupDataSize, segments);
            HashGroup.Write(data, clusters).CopyTo(h3, g * DiscFormat.HashSize);
            output.Write(clusters, 0, clusters.Length);
        }
    }

    private void WriteDiscHeader(Stream output)
    {
        var header = new byte[0x100];
        Encoding.ASCII.GetBytes(GameId).CopyTo(header, 0);
        header[6] = DiscNumber;
        header[7] = Version;
        BigEndian.WriteUInt32(header, 0x18, DiscFormat.WiiMagic);
        Encoding.ASCII.GetBytes(Title).CopyTo(header, 0x20);
        WriteAt(output, 0, header);

        var table = new byte[PartitionTable.Size];
        BigEndian.WriteUInt32(table, 0, 1);
        BigEndian.WriteUInt32(table, 4, (uint)((DiscFormat.PartitionTableOffset + PartitionTable.Size) >> 2));
        WriteAt(output, DiscFormat.PartitionTableOffset, table);
        var entry = new byte[DiscFormat.PartitionEntrySize];
        BigEndian.WriteUInt32(entry, 0, (uint)(PartitionOffset >> 2));
        BigEndian.WriteUInt32(entry, 4, (uint)PartitionType.Data);
        WriteAt(output, DiscFormat.PartitionTableOffset + PartitionTable.Size, entry);

        WriteAt(output, RegionArea.Offset, Region.ToBytes());
        var magic = new byte[4];
        BigEndian.WriteUInt32(magic, 0, DiscFormat.DiscMagic);
        WriteAt(output, DiscFormat.DiscMagicOffset, magic);
    }

    private void WritePartitionHead(Stream output, Ticket ticket, Tmd tmd, byte[] h3, long clusterBytes)
    {
        var chain = System.CertificateChain;
        var certOffset = Align(TmdOffset + tmd.Size, SystemAlignment);
        if (certOffset + chain.Length > DiscFormat.H3Offset)
            throw new InvalidDataException("Certificate chain does not fit before the H3 table.");

        var header = new byte[PartitionHeader.Size];
        BigEndian.WriteUInt32(header, 0x00, (uint)tmd.Size);
        BigEndian.WriteUInt32(header, 0x04, TmdOffset >> 2);
        BigEndian.WriteUInt32(header, 0x08, (uint)chain.Length);
        BigEndian.WriteUInt32(header, 0x0C, (uint)(certOffset >> 2));
        BigEndian.WriteUInt32(header, 0x10, (uint)(DiscFormat.H3Offset >> 2));
        BigEndian.WriteUInt32(header, 0x14, (uint)(DiscFormat.PartitionDataOffset >> 2));
        BigEndian.WriteUInt32(header, 0x18, (uint)(clusterBytes >> 2));

        WriteAt(output, PartitionOffset, ticket.ToBytes());
        WriteAt(output, PartitionOffset + Ticket.Size, header);
        WriteAt(output, PartitionOffset + TmdOffset, tmd.ToBytes());
        WriteAt(output, PartitionOffset + certOffset, chain);
        WriteAt(output, PartitionOffset + DiscFormat.H3Offset, h3);
    }

    private sealed class Segment
    {
        private readonly byte[]? _bytes;
        private readonly Stream? _stream;

        public Segment(long offset, byte[] bytes)
        {
            Offset = offset;
            Length = bytes.Length;
            _bytes = bytes;
        }

        public Segment(long offset, Stream stream)
        {
            Offset = offset;
            Length = stream.Length;
            _stream = stream;
        }

        public long Length { get; }
        public long Offset { get; }

        public void Read(long from, byte[] buffer, int at, int count)
        {
            if (_bytes is not null)
            {
                Array.Copy(_bytes, from, buffer, at, count);
                return;
            }
            _stream!.Position = from;
            _stream.ReadExactly(buffer, at, count);
        }
    }
}
