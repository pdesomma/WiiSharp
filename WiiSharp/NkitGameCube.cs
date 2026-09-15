namespace WiiSharp;

/// <summary>
/// Converts GameCube images to and from NKit: files are packed back to back with every gap reduced to a record that regenerates it, so the compact image still boots on hardware and restores bit for bit.
/// </summary>
public static class NkitGameCube
{
    /// <summary>
    /// Files that start on a 32 KiB boundary and span whole 32 KiB blocks keep that alignment, padded with zeros.
    /// </summary>
    public const int PreservedAlignment = 0x8000;
    /// <summary>
    /// Compact images end on this boundary.
    /// </summary>
    public const int EndAlignment = 0x800;

    private const int DolOffsetField = 0x420;
    private const int FstOffsetField = 0x424;
    private const int FstSizeField = 0x428;
    private const int ScratchSize = 1 << 20;

    /// <summary>
    /// Writes the compact form of a full image.
    /// </summary>
    /// <param name="image">Seekable full image.</param>
    /// <param name="output">Seekable, writable destination.</param>
    /// <param name="cancellationToken">Cancels between blocks.</param>
    /// <returns>The header written at 0x200.</returns>
    /// <exception cref="InvalidDataException">Not a GameCube image, already compact, or files overlap.</exception>
    public static NkitHeader Compact(Stream image, Stream output, CancellationToken cancellationToken = default)
    {
        if (image is null)
            throw new ArgumentNullException(nameof(image));
        if (output is null)
            throw new ArgumentNullException(nameof(output));
        if (!output.CanSeek || !output.CanWrite)
            throw new ArgumentException("Output must be writable and seekable.", nameof(output));

        var boot = image.ReadExactlyAt(0, DiscFormat.BootSize);
        var header = DiscHeader.Parse(boot);
        if (!header.IsGameCube)
            throw new InvalidDataException("Not a GameCube image.");
        if (NkitHeader.IsPresent(boot))
            throw new InvalidDataException("Image is already NKit.");

        var fstOffset = (long)BigEndian.ReadUInt32(boot, FstOffsetField);
        var fstSize = checked((int)BigEndian.ReadUInt32(boot, FstSizeField));
        var fst = image.ReadExactlyAt(fstOffset, fstSize);
        var entries = Fst.FileEntries(fst).Where(e => e.Length > 0).OrderBy(e => e.RawOffset).ToList();
        var fstEnd = fstOffset + fstSize;
        if (entries.Count > 0 && entries[0].RawOffset < fstEnd)
            throw new InvalidDataException("A file overlaps the file system table.");

        var codec = new NkitGapCodec(new JunkGenerator(boot, header.DiscNumber));
        var scratch = new byte[ScratchSize];
        var sourceLength = image.Length;

        // everything up to the end of the table stays where it is; the table itself is rewritten last
        output.SetLength(0);
        output.Position = 0;
        image.Position = 0;
        var crc = CopyWithCrc(image, output, fstEnd, scratch, Crc32.Initial, cancellationToken);

        var sourceEnd = fstEnd;
        var afterFst = true;
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long offset = entry.RawOffset;
            if (offset < sourceEnd)
                throw new InvalidDataException("Files overlap.");

            crc = CopyPadding(image, output, sourceEnd, offset, scratch, crc, cancellationToken);
            var gapStart = Align(sourceEnd, 4);
            if (offset > gapStart)
                crc = codec.Encode(image, gapStart, offset - gapStart, afterFst, output, scratch, crc, cancellationToken);
            afterFst = false;

            if (offset % PreservedAlignment == 0 && entry.Length % PreservedAlignment == 0)
                WriteZeros(output, Align(output.Position, PreservedAlignment) - output.Position, scratch);

            Fst.SetFileEntry(fst, entry.Index, (uint)output.Position, entry.Length);
            image.Position = offset;
            crc = CopyWithCrc(image, output, entry.Length, scratch, crc, cancellationToken);
            sourceEnd = offset + entry.Length;
        }

        crc = CopyPadding(image, output, sourceEnd, sourceLength, scratch, crc, cancellationToken);
        var tailStart = Align(sourceEnd, 4);
        if (sourceLength > tailStart)
            crc = codec.Encode(image, tailStart, sourceLength - tailStart, afterFst, output, scratch, crc, cancellationToken);
        WriteZeros(output, Align(output.Position, EndAlignment) - output.Position, scratch);

        output.Position = fstOffset;
        output.Write(fst, 0, fst.Length);

        var nkit = new NkitHeader(isWii: false, Crc32.Finish(crc), sourceLength);
        nkit.Write(boot);
        output.Position = 0;
        output.Write(boot, 0, boot.Length);
        PatchCrc(output, nkit.SourceCrc, scratch, cancellationToken);
        return NkitHeader.Parse(output.ReadExactlyAt(0, DiscFormat.BootSize));
    }

    /// <summary>
    /// Writes the full image a compact one stands for and checks it against the recorded CRC-32.
    /// </summary>
    /// <param name="nkit">Seekable compact image.</param>
    /// <param name="output">Seekable, writable destination.</param>
    /// <param name="cancellationToken">Cancels between blocks.</param>
    /// <exception cref="InvalidDataException">Not a compact GameCube image, or the result does not match its CRC-32.</exception>
    public static void Restore(Stream nkit, Stream output, CancellationToken cancellationToken = default)
    {
        if (nkit is null)
            throw new ArgumentNullException(nameof(nkit));
        if (output is null)
            throw new ArgumentNullException(nameof(output));
        if (!output.CanSeek || !output.CanWrite)
            throw new ArgumentException("Output must be writable and seekable.", nameof(output));

        var boot = nkit.ReadExactlyAt(0, DiscFormat.BootSize);
        var header = DiscHeader.Parse(boot);
        if (!header.IsGameCube)
            throw new InvalidDataException("Not a GameCube image.");
        var info = NkitHeader.Parse(boot);

        var fstOffset = (long)BigEndian.ReadUInt32(boot, FstOffsetField);
        var fstSize = checked((int)BigEndian.ReadUInt32(boot, FstSizeField));
        var fst = nkit.ReadExactlyAt(fstOffset, fstSize);
        var files = Fst.FileEntries(fst);
        var entries = files.Where(e => e.Length > 0).OrderBy(e => e.RawOffset).ToList();
        var junkFiles = new Queue<FstEntry>(files.Where(e => e.Length == 0));
        var fstEnd = fstOffset + fstSize;

        var junkId = new byte[4];
        BigEndian.WriteUInt32(junkId, 0, info.ForcedJunkId);
        var codec = new NkitGapCodec(new JunkGenerator(info.ForcedJunkId == 0 ? boot : junkId, header.DiscNumber));
        var scratch = new byte[ScratchSize];

        output.SetLength(0);
        output.Position = 0;
        nkit.Position = 0;
        nkit.CopyExactly(output, fstEnd, scratch, cancellationToken);

        var nkitEnd = fstEnd;
        var afterFst = true;
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long offset = entry.RawOffset;
            nkit.Position = nkitEnd;
            nkit.CopyExactly(output, Math.Min(Align(nkitEnd, 4), offset) - nkitEnd, scratch, cancellationToken);
            var at = Align(nkitEnd, 4);
            while (at < offset && PeekWord(nkit, at) != 0)
            {
                nkit.Position = at;
                afterFst = ExpandRecord(nkit, output, codec, fst, junkFiles, afterFst, scratch, cancellationToken);
                at = nkit.Position;
            }
            afterFst = false;

            Fst.SetFileEntry(fst, entry.Index, (uint)output.Position, entry.Length);
            nkit.Position = offset;
            nkit.CopyExactly(output, entry.Length, scratch, cancellationToken);
            nkitEnd = offset + entry.Length;
        }

        nkit.Position = nkitEnd;
        nkit.CopyExactly(output, Math.Min(Align(nkitEnd, 4), nkit.Length) - nkitEnd, scratch, cancellationToken);
        while (output.Position < info.SourceLength)
        {
            nkit.Position = Align(nkitEnd, 4);
            afterFst = ExpandRecord(nkit, output, codec, fst, junkFiles, afterFst, scratch, cancellationToken);
            nkitEnd = nkit.Position;
        }
        if (output.Position != info.SourceLength)
            throw new InvalidDataException("Expanded image overruns the recorded length.");

        output.Position = fstOffset;
        output.Write(fst, 0, fst.Length);
        Array.Clear(boot, NkitHeader.Offset, NkitHeader.Size);
        output.Position = 0;
        output.Write(boot, 0, boot.Length);

        output.Position = 0;
        var crc = CopyWithCrc(output, Stream.Null, info.SourceLength, scratch, Crc32.Initial, cancellationToken);
        if (Crc32.Finish(crc) != info.SourceCrc)
            throw new InvalidDataException($"Expanded image CRC-32 {Crc32.Finish(crc):X8} does not match the recorded {info.SourceCrc:X8}.");
    }

    private static long Align(long value, long alignment) => (value + alignment - 1) / alignment * alignment;

    /// <summary>
    /// Copies the 1-3 zero bytes that round a file up to 4; feeds them through the CRC.
    /// </summary>
    private static uint CopyPadding(Stream image, Stream output, long fileEnd, long limit, byte[] scratch, uint crc, CancellationToken cancellationToken)
    {
        var count = Math.Min(Align(fileEnd, 4), limit) - fileEnd;
        if (count <= 0)
            return crc;
        image.Position = fileEnd;
        return CopyWithCrc(image, output, count, scratch, crc, cancellationToken);
    }

    private static uint CopyWithCrc(Stream source, Stream destination, long count, byte[] scratch, uint crc, CancellationToken cancellationToken)
    {
        while (count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var n = source.Read(scratch, 0, (int)Math.Min(scratch.Length, count));
            if (n == 0)
                throw new EndOfStreamException("Image ends early.");
            crc = Crc32.Update(crc, scratch, 0, n);
            destination.Write(scratch, 0, n);
            count -= n;
        }
        return crc;
    }

    /// <summary>
    /// Expands the record at the current position; a junk-file record also claims the next empty table entry.
    /// </summary>
    /// <returns>False: the gap after the table has now been passed.</returns>
    private static bool ExpandRecord(Stream nkit, Stream output, NkitGapCodec codec, byte[] fst, Queue<FstEntry> junkFiles, bool afterFst, byte[] scratch, CancellationToken cancellationToken)
    {
        var start = output.Position;
        var (size, junkFile) = codec.Decode(nkit, output, start, afterFst, scratch, cancellationToken);
        if (junkFile)
        {
            if (junkFiles.Count == 0)
                throw new InvalidDataException("Junk file record without an empty table entry.");
            Fst.SetFileEntry(fst, junkFiles.Dequeue().Index, (uint)start, (uint)size);
        }
        return false;
    }

    /// <summary>
    /// Rewrites the four bytes at 0x20C so the whole compact file checksums to the source CRC-32.
    /// </summary>
    private static void PatchCrc(Stream output, uint target, byte[] scratch, CancellationToken cancellationToken)
    {
        output.Position = 0;
        var before = CopyWithCrc(output, Stream.Null, NkitHeader.PatchOffset, scratch, Crc32.Initial, cancellationToken);

        // run the register backwards from the end of the file to just after the patch
        var register = target ^ Crc32.Initial;
        var position = output.Length;
        while (position > NkitHeader.PatchOffset + 4)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var n = (int)Math.Min(scratch.Length, position - (NkitHeader.PatchOffset + 4));
            output.Position = position - n;
            output.ReadExactly(scratch, 0, n);
            for (var i = n - 1; i >= 0; i--)
                register = Crc32.Rewind(register, scratch[i]);
            position -= n;
        }

        output.Position = NkitHeader.PatchOffset;
        output.Write(Crc32.Patch(before, register), 0, 4);
    }

    private static uint PeekWord(Stream stream, long position) =>
        position + 4 <= stream.Length ? BigEndian.ReadUInt32(stream.ReadExactlyAt(position, 4), 0) : 0;

    private static void WriteZeros(Stream output, long count, byte[] scratch)
    {
        Array.Clear(scratch, 0, (int)Math.Min(scratch.Length, count));
        while (count > 0)
        {
            var n = (int)Math.Min(scratch.Length, count);
            output.Write(scratch, 0, n);
            count -= n;
        }
    }
}
