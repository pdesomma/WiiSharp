using System.Text;

namespace WiiSharp;

/// <summary>
/// First bytes of a disc: game ID, disc number and title.
/// </summary>
public sealed class DiscHeader
{
    /// <summary>
    /// Bytes <see cref="Parse"/> needs.
    /// </summary>
    public const int Size = 0x60;

    private DiscHeader(string gameId, byte discNumber, byte version, string title, bool isWii, bool isGameCube)
    {
        GameId = gameId;
        DiscNumber = discNumber;
        Version = version;
        Title = title;
        IsWii = isWii;
        IsGameCube = isGameCube;
    }

    /// <summary>
    /// Zero-based disc number for multi-disc games.
    /// </summary>
    public byte DiscNumber { get; }
    /// <summary>
    /// Six-character game ID, e.g. RMCE01.
    /// </summary>
    public string GameId { get; }
    /// <summary>
    /// GameCube magic present.
    /// </summary>
    public bool IsGameCube { get; }
    /// <summary>
    /// Wii magic present.
    /// </summary>
    public bool IsWii { get; }
    /// <summary>
    /// Game title from the header.
    /// </summary>
    public string Title { get; }
    /// <summary>
    /// Disc revision.
    /// </summary>
    public byte Version { get; }

    /// <summary>
    /// Parses the start of a disc.
    /// </summary>
    /// <param name="bytes">At least <see cref="Size"/> bytes.</param>
    public static DiscHeader Parse(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length < Size)
            throw new ArgumentException($"Need at least {Size} bytes.", nameof(bytes));

        return new DiscHeader(
            Encoding.ASCII.GetString(bytes, 0, 6),
            bytes[6],
            bytes[7],
            Encoding.ASCII.GetString(bytes, 0x20, 0x40).TrimEnd('\0'),
            BigEndian.ReadUInt32(bytes, 0x18) == DiscFormat.WiiMagic,
            BigEndian.ReadUInt32(bytes, 0x1C) == DiscFormat.GameCubeMagic);
    }
}
