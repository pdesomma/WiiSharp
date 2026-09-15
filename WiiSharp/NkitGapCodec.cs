namespace WiiSharp;

/// <summary>
/// NKit's run-length code for the space between files: a gap that is all regenerable junk or all zeros shrinks to one word; anything else keeps its odd 256-byte blocks.
/// </summary>
internal sealed class NkitGapCodec
{
    public const int BlockSize = 256;
    public const int TrailingNulls = 28;
    public const long LargeGap = 0x40000;
    public const uint ExtendedSize = 0xFFFFFFFC;

    private const uint KindMask = 3;
    private const uint BlockKindMask = 0xC0000000;
    private const uint BlockCountMask = 0x3FFFFFFF;
    private const uint FillCountMask = 0x3FFFFF00;
    private const uint BlockJunk = 0x00000000;
    private const uint BlockNonJunk = 0x40000000;
    private const uint BlockFill = 0x80000000;
    private const uint BlockRepeat = 0xC0000000;

    private readonly JunkGenerator _junk;

    /// <summary>
    /// Creates a new instance of the <see cref="NkitGapCodec"/> class.
    /// </summary>
    /// <param name="junk">Junk stream of the disc or partition the gaps sit in.</param>
    public NkitGapCodec(JunkGenerator junk)
    {
        _junk = junk ?? throw new ArgumentNullException(nameof(junk));
    }

    /// <summary>
    /// Zero bytes the mastering wrote at the head of a gap before the junk starts.
    /// </summary>
    /// <param name="gapSize">Gap length.</param>
    /// <param name="afterFst">True for the gap that follows the file system table.</param>
    public static int NullsFor(long gapSize, bool afterFst) =>
        afterFst || gapSize <= LargeGap ? (int)Math.Min(TrailingNulls, gapSize) : 0;

    /// <summary>
    /// Expands one gap record; leaves <paramref name="nkit"/> after the record and its preserved blocks.
    /// </summary>
    /// <param name="nkit">Compact image positioned at the record.</param>
    /// <param name="output">Receives the gap bytes.</param>
    /// <param name="gapStart">Where the gap sits in the full image.</param>
    /// <param name="afterFst">True for the gap that follows the file system table.</param>
    /// <param name="scratch">Buffer of at least 256 bytes.</param>
    /// <param name="cancellationToken">Cancels between blocks.</param>
    /// <returns>Gap length and whether the record stood for a junk file rather than a gap.</returns>
    public (long Size, bool JunkFile) Decode(Stream nkit, Stream output, long gapStart, bool afterFst, byte[] scratch, CancellationToken cancellationToken)
    {
        var word = ReadWord(nkit);
        long size = word & ~KindMask;
        if ((word & ~KindMask) == ExtendedSize)
            size += ReadWord(nkit);
        var kind = (int)(word & KindMask);
        if (size == 0)
            throw new InvalidDataException("Empty gap record.");
        var nulls = NullsFor(size, afterFst);

        switch (kind)
        {
            case 0:
            case 3:
                WriteExpected(output, gapStart, nulls, gapStart, size, scratch, cancellationToken);
                return (size, kind == 3);
            case 1:
                WriteFill(output, 0, size, scratch, cancellationToken);
                return (size, false);
        }

        long done = 0;
        while (done < size)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var header = ReadWord(nkit);
            var blockKind = header & BlockKindMask;
            var blocks = blockKind == BlockFill ? (header & FillCountMask) >> 8 : header & BlockCountMask;
            var length = Math.Min(blocks * (long)BlockSize, size - done);
            switch (blockKind)
            {
                case BlockJunk:
                    WriteExpected(output, gapStart, nulls, gapStart + done, length, scratch, cancellationToken);
                    break;
                case BlockNonJunk:
                    nkit.CopyExactly(output, length, scratch, cancellationToken);
                    break;
                case BlockFill:
                    WriteFill(output, (byte)header, length, scratch, cancellationToken);
                    break;
                default:
                    throw new NotSupportedException("NKit repeat blocks are not supported.");
            }
            done += length;
        }
        return (size, false);
    }

    /// <summary>
    /// Writes one gap record for the bytes at <paramref name="gapStart"/> of <paramref name="source"/>.
    /// </summary>
    /// <param name="source">Full image.</param>
    /// <param name="gapStart">Where the gap sits.</param>
    /// <param name="gapSize">Gap length.</param>
    /// <param name="afterFst">True for the gap that follows the file system table.</param>
    /// <param name="output">Seekable compact image positioned where the record goes.</param>
    /// <param name="scratch">Buffer whose length is a positive multiple of 256.</param>
    /// <param name="crc">CRC-32 register so far.</param>
    /// <param name="cancellationToken">Cancels between blocks.</param>
    /// <returns>Register after feeding the gap bytes through <see cref="Crc32.Update"/> from <paramref name="crc"/>.</returns>
    public uint Encode(Stream source, long gapStart, long gapSize, bool afterFst, Stream output, byte[] scratch, uint crc, CancellationToken cancellationToken)
    {
        if (scratch.Length < BlockSize || scratch.Length % BlockSize != 0)
            throw new ArgumentException("Scratch must be a positive multiple of 256 bytes.", nameof(scratch));

        var nulls = NullsFor(gapSize, afterFst);
        var expected = new byte[scratch.Length];
        var writer = new RecordWriter(output, gapSize);
        source.Position = gapStart;
        long done = 0;
        while (done < gapSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = (int)Math.Min(scratch.Length, gapSize - done);
            source.ReadExactly(scratch, 0, count);
            crc = Crc32.Update(crc, scratch, 0, count);
            FillExpected(gapStart, nulls, gapStart + done, expected, 0, count);
            for (var at = 0; at < count; at += BlockSize)
            {
                var length = Math.Min(BlockSize, count - at);
                writer.Add(Classify(scratch, expected, at, length), scratch, at, length);
            }
            done += count;
        }
        writer.Finish();
        return crc;
    }

    private static (uint Kind, byte Fill) Classify(byte[] data, byte[] expected, int at, int length)
    {
        var junk = true;
        var fill = true;
        for (var i = at; i < at + length; i++)
        {
            junk &= data[i] == expected[i];
            fill &= data[i] == data[at];
            if (!junk && !fill)
                return (BlockNonJunk, (byte)0);
        }
        return junk ? (BlockJunk, (byte)0) : (BlockFill, data[at]);
    }

    private static uint ReadWord(Stream stream) => BigEndian.ReadUInt32(stream.ReadExactly(4), 0);

    private static void WriteFill(Stream output, byte value, long count, byte[] scratch, CancellationToken cancellationToken)
    {
        for (var i = 0; i < scratch.Length; i++)
            scratch[i] = value;
        while (count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var n = (int)Math.Min(scratch.Length, count);
            output.Write(scratch, 0, n);
            count -= n;
        }
    }

    private void FillExpected(long gapStart, int nulls, long position, byte[] buffer, int offset, int count)
    {
        var zeros = (int)Math.Max(0, Math.Min(count, gapStart + nulls - position));
        Array.Clear(buffer, offset, zeros);
        if (count > zeros)
            _junk.Fill(position + zeros, buffer, offset + zeros, count - zeros);
    }

    private void WriteExpected(Stream output, long gapStart, int nulls, long position, long count, byte[] scratch, CancellationToken cancellationToken)
    {
        while (count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var n = (int)Math.Min(scratch.Length, count);
            FillExpected(gapStart, nulls, position, scratch, 0, n);
            output.Write(scratch, 0, n);
            position += n;
            count -= n;
        }
    }

    /// <summary>
    /// Streams a record: holds uniform runs back until the gap proves mixed, then spills them as block headers.
    /// </summary>
    private sealed class RecordWriter
    {
        private readonly long _gapSize;
        private readonly Stream _output;
        private readonly long _recordAt;
        private readonly List<(uint Kind, byte Fill, long Blocks)> _pending = new();
        private bool _mixed;
        private long _openCountAt = -1;
        private long _openBlocks;
        private uint _openKind;
        private byte _openFill;

        /// <summary>
        /// Creates a new instance of the <see cref="RecordWriter"/> class; reserves the size word.
        /// </summary>
        /// <param name="output">Seekable compact image positioned where the record goes.</param>
        /// <param name="gapSize">Gap length.</param>
        public RecordWriter(Stream output, long gapSize)
        {
            _output = output;
            _gapSize = gapSize;
            _recordAt = output.Position;
            WriteWord(0);
            if (gapSize >= ExtendedSize)
                WriteWord(0);
        }

        /// <summary>
        /// Appends one classified block.
        /// </summary>
        /// <param name="block">Kind and fill byte.</param>
        /// <param name="data">Block bytes, kept only for non-junk.</param>
        /// <param name="at">First byte.</param>
        /// <param name="length">Block length; the last block may be short.</param>
        public void Add((uint Kind, byte Fill) block, byte[] data, int at, int length)
        {
            if (_openBlocks > 0 && block.Kind == _openKind && block.Fill == _openFill && _openBlocks < LimitFor(_openKind))
            {
                _openBlocks++;
                if (block.Kind == BlockNonJunk)
                    _output.Write(data, at, length);
                return;
            }

            CloseRun();
            if (block.Kind == BlockNonJunk || _pending.Count > 0)
                Spill();
            _openKind = block.Kind;
            _openFill = block.Fill;
            _openBlocks = 1;
            if (!_mixed)
                return;
            _openCountAt = _output.Position;
            WriteWord(0);
            if (block.Kind == BlockNonJunk)
                _output.Write(data, at, length);
        }

        /// <summary>
        /// Writes the size word once the whole gap is classified.
        /// </summary>
        public void Finish()
        {
            CloseRun();
            uint kind;
            if (_mixed)
                kind = 2;
            else if (_pending.Count == 0 || _pending[0].Kind == BlockJunk)
                kind = 0;
            else if (_pending[0].Kind == BlockFill && _pending[0].Fill == 0)
                kind = 1;
            else
            {
                Spill();
                kind = 2;
            }

            var end = _output.Position;
            _output.Position = _recordAt;
            if (_gapSize >= ExtendedSize)
            {
                WriteWord(ExtendedSize | kind);
                WriteWord((uint)(_gapSize - ExtendedSize));
            }
            else
                WriteWord((uint)_gapSize | kind);
            _output.Position = end;
        }

        private static uint HeaderFor(uint kind, byte fill, long blocks) =>
            kind == BlockFill ? kind | (uint)blocks << 8 | fill : kind | (uint)blocks;

        private static long LimitFor(uint kind) => kind == BlockFill ? FillCountMask >> 8 : BlockCountMask;

        private void CloseRun()
        {
            if (_openBlocks == 0)
                return;
            if (_mixed)
            {
                var end = _output.Position;
                _output.Position = _openCountAt;
                WriteWord(HeaderFor(_openKind, _openFill, _openBlocks));
                _output.Position = end;
            }
            else
                _pending.Add((_openKind, _openFill, _openBlocks));
            _openBlocks = 0;
        }

        /// <summary>
        /// Commits to a mixed record: writes every held run as a block header.
        /// </summary>
        private void Spill()
        {
            if (_mixed)
                return;
            _mixed = true;
            foreach (var run in _pending)
                WriteWord(HeaderFor(run.Kind, run.Fill, run.Blocks));
            _pending.Clear();
        }

        private void WriteWord(uint value)
        {
            var bytes = new byte[4];
            BigEndian.WriteUInt32(bytes, 0, value);
            _output.Write(bytes, 0, 4);
        }
    }
}
