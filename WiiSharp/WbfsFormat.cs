using System.Globalization;

namespace WiiSharp;

/// <summary>
/// Constants of the WBFS container (libwbfs).
/// </summary>
public static class WbfsFormat
{
    /// <summary>
    /// Bytes of the disc header kept at the start of each disc slot.
    /// </summary>
    public const int DiscHeaderSize = 0x100;
    /// <summary>
    /// Bytes a full dual-layer disc occupies: <see cref="WiiSectorsPerDisc"/> sectors of 0x8000.
    /// </summary>
    public const long DiscSize = (long)WiiSectorsPerDisc << WiiSectorShift;
    /// <summary>
    /// File extension of the first part.
    /// </summary>
    public const string Extension = ".wbfs";
    /// <summary>
    /// Bytes before the disc table.
    /// </summary>
    public const int HeaderSize = 12;
    /// <summary>
    /// "WBFS".
    /// </summary>
    public const uint Magic = 0x57424653;
    /// <summary>
    /// Log2 of the 0x8000-byte Wii sector.
    /// </summary>
    public const int WiiSectorShift = 15;
    /// <summary>
    /// Wii sectors libwbfs reserves per disc (dual layer).
    /// </summary>
    public const int WiiSectorsPerDisc = 143432 * 2;

    /// <summary>
    /// Name of a split part: the .wbfs itself for part 0, then .wbs1, .wbs2 …
    /// </summary>
    /// <param name="path">Path of the .wbfs file.</param>
    /// <param name="part">Zero-based part number.</param>
    public static string PartPath(string path, int part)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));
        if (part < 0)
            throw new ArgumentOutOfRangeException(nameof(part));

        return part == 0 ? path : Path.ChangeExtension(path, ".wbs" + part.ToString(CultureInfo.InvariantCulture));
    }
}
