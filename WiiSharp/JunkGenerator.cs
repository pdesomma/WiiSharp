namespace WiiSharp;

/// <summary>
/// Regenerates the filler Nintendo's mastering wrote between files: a lagged Fibonacci stream reseeded every 32 KiB sector from the disc ID, disc number and sector index.
/// </summary>
public sealed class JunkGenerator
{
    /// <summary>
    /// Bytes per reseed.
    /// </summary>
    public const int SectorSize = 0x8000;

    private const int K = 521;
    private const int J = 32;
    private const int SeedWords = 17;
    private const int BufferBytes = K * 4;

    private readonly byte[] _bytes = new byte[BufferBytes];
    private readonly uint _seed;
    private readonly uint[] _words = new uint[K];
    private int _consumed;
    private int _position;
    private long _sector = -1;

    /// <summary>
    /// Creates a new instance of the <see cref="JunkGenerator"/> class.
    /// </summary>
    /// <param name="discId">Disc ID; the first four bytes seed the stream.</param>
    /// <param name="discNumber">Disc number from the header.</param>
    public JunkGenerator(byte[] discId, byte discNumber)
    {
        if (discId is null)
            throw new ArgumentNullException(nameof(discId));
        if (discId.Length < 4)
            throw new ArgumentException("A disc ID is at least 4 bytes.", nameof(discId));

        _seed = ((uint)discId[2] << 24 | (uint)discId[1] << 16 | (uint)(byte)(discId[3] + discId[2]) << 8 | (byte)(discId[0] + discId[1])) ^ discNumber;
    }

    /// <summary>
    /// Writes the junk that belongs at <paramref name="position"/> on the disc (or in the partition data, for Wii).
    /// </summary>
    /// <param name="position">Absolute position of the first byte.</param>
    /// <param name="buffer">Destination.</param>
    /// <param name="offset">First byte to write.</param>
    /// <param name="count">Bytes to write.</param>
    public void Fill(long position, byte[] buffer, int offset, int count)
    {
        if (buffer is null)
            throw new ArgumentNullException(nameof(buffer));
        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        while (count > 0)
        {
            Seek(position);
            var chunk = (int)Math.Min(count, SectorSize - position % SectorSize);
            var written = 0;
            while (written < chunk)
            {
                if (_position == BufferBytes)
                    Forward();
                var n = Math.Min(chunk - written, BufferBytes - _position);
                Array.Copy(_bytes, _position, buffer, offset + written, n);
                _position += n;
                _consumed += n;
                written += n;
            }
            position += chunk;
            offset += chunk;
            count -= chunk;
        }
    }

    /// <summary>
    /// Counts how many leading bytes of <paramref name="data"/> equal the junk that belongs at <paramref name="position"/>.
    /// </summary>
    /// <param name="position">Absolute position of the first byte.</param>
    /// <param name="data">Bytes to compare.</param>
    /// <param name="offset">First byte to compare.</param>
    /// <param name="count">Bytes to compare.</param>
    public int Match(long position, byte[] data, int offset, int count)
    {
        if (data is null)
            throw new ArgumentNullException(nameof(data));
        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position));
        if (offset < 0 || count < 0 || offset + count > data.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        var matched = 0;
        while (matched < count)
        {
            Seek(position);
            var chunk = (int)Math.Min(count - matched, SectorSize - position % SectorSize);
            var compared = 0;
            while (compared < chunk)
            {
                if (_position == BufferBytes)
                    Forward();
                var n = Math.Min(chunk - compared, BufferBytes - _position);
                for (var i = 0; i < n; i++)
                {
                    if (_bytes[_position + i] != data[offset + matched + compared + i])
                        return matched + compared + i;
                }
                _position += n;
                _consumed += n;
                compared += n;
            }
            position += chunk;
            matched += chunk;
        }
        return matched;
    }

    private static uint Swap(uint value) =>
        value << 24 | (value & 0xFF00) << 8 | (value >> 8) & 0xFF00 | value >> 24;

    private void Forward()
    {
        for (var i = 0; i < J; i++)
            _words[i] ^= _words[i + K - J];
        for (var i = J; i < K; i++)
            _words[i] ^= _words[i - J];
        Buffer.BlockCopy(_words, 0, _bytes, 0, BufferBytes);
        _position = 0;
    }

    private void Reseed(long sector)
    {
        // the seed words come from a linear congruential generator keyed by disc ID and sector
        var n = unchecked(_seed * 0x260BCD5) ^ unchecked((uint)sector * 0x1EF29123);
        for (var i = 0; i < SeedWords; i++)
        {
            uint word = 0;
            for (var j = 0; j < J; j++)
            {
                n = unchecked(n * 0x5D588B65 + 1);
                word = word >> 1 | n & 0x80000000;
            }
            _words[i] = word;
        }
        _words[16] ^= _words[0] >> 9 ^ _words[16] << 23;

        for (var i = SeedWords; i < K; i++)
            _words[i] = _words[i - SeedWords] << 23 ^ _words[i - SeedWords + 1] >> 9 ^ _words[i - 1];
        // the hardware emits the words big-endian with bits 16-17 dropped
        for (var i = 0; i < K; i++)
            _words[i] = Swap(_words[i] & 0xFF00FFFF | _words[i] >> 2 & 0x00FF0000);
        for (var i = 0; i < 4; i++)
            Forward();
        _consumed = 0;
        _sector = sector;
    }

    private void Seek(long position)
    {
        var sector = position / SectorSize;
        var within = (int)(position % SectorSize);
        if (sector != _sector || within < _consumed)
            Reseed(sector);
        Skip(within - _consumed);
    }

    private void Skip(int count)
    {
        while (count > 0)
        {
            if (_position == BufferBytes)
                Forward();
            var n = Math.Min(count, BufferBytes - _position);
            _position += n;
            _consumed += n;
            count -= n;
        }
    }
}
