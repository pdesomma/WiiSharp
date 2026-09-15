namespace WiiSharp;

/// <summary>
/// Reflected CRC-32 (IEEE), the checksum Redump and NKit use for disc images.
/// </summary>
public static class Crc32
{
    /// <summary>
    /// Register value before any byte is fed.
    /// </summary>
    public const uint Initial = 0xFFFFFFFF;

    private static readonly uint[] Table = BuildTable();

    /// <summary>
    /// Final checksum for a register.
    /// </summary>
    /// <param name="register">Register after every byte was fed.</param>
    public static uint Finish(uint register) => register ^ Initial;

    /// <summary>
    /// Feeds bytes through the register.
    /// </summary>
    /// <param name="register">Current register; start with <see cref="Initial"/>.</param>
    /// <param name="bytes">Source.</param>
    /// <param name="offset">First byte to feed.</param>
    /// <param name="count">Bytes to feed.</param>
    public static uint Update(uint register, byte[] bytes, int offset, int count)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (offset < 0 || count < 0 || offset + count > bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        for (var i = offset; i < offset + count; i++)
            register = Table[(register ^ bytes[i]) & 0xFF] ^ register >> 8;
        return register;
    }

    /// <summary>
    /// Undoes the feeding of one byte: the register before <paramref name="value"/> was fed.
    /// </summary>
    /// <param name="register">Register after the byte.</param>
    /// <param name="value">The byte that was fed.</param>
    public static uint Rewind(uint register, byte value)
    {
        // the table's high bytes are distinct, so the top byte names the entry that was applied
        var index = 0;
        while (Table[index] >> 24 != register >> 24)
            index++;
        return (register ^ Table[index]) << 8 | (uint)((index ^ value) & 0xFF);
    }

    /// <summary>
    /// Four bytes that, placed after <paramref name="before"/> and ahead of the bytes that led to <paramref name="after"/>, make the stream checksum as intended.
    /// </summary>
    /// <param name="before">Register after everything ahead of the patch.</param>
    /// <param name="after">Register the patch must leave behind, as rewound from the intended final register through everything after the patch.</param>
    public static byte[] Patch(uint before, uint after)
    {
        // feeding four bytes equals feeding four zeros into the register XOR those bytes little-endian
        var zeros = after;
        for (var i = 0; i < 4; i++)
            zeros = Rewind(zeros, 0);
        var word = before ^ zeros;
        return new[] { (byte)word, (byte)(word >> 8), (byte)(word >> 16), (byte)(word >> 24) };
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ c >> 1 : c >> 1;
            table[i] = c;
        }
        return table;
    }
}
