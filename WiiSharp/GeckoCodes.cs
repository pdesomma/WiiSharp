using System.Globalization;
using System.Text.RegularExpressions;

namespace WiiSharp;

/// <summary>
/// Gecko cheat codes: lines of two 32-bit words, read from a .gct or from Ocarina / Dolphin text, written back as a .gct.
/// </summary>
public static class GeckoCodes
{
    /// <summary>
    /// The eight bytes a .gct starts with.
    /// </summary>
    public static readonly byte[] Magic = { 0x00, 0xD0, 0xC0, 0xDE, 0x00, 0xD0, 0xC0, 0xDE };
    /// <summary>
    /// The eight bytes a .gct ends with.
    /// </summary>
    public static readonly byte[] Terminator = { 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

    private const int LineSize = 8;
    private static readonly Regex CodeLine = new(@"^\s*(?<left>[0-9A-Fa-f]{8})\s+(?<right>[0-9A-Fa-f]{8})\s*$", RegexOptions.CultureInvariant);
    private static readonly Regex SectionLine = new(@"^\s*\[(?<name>[^\]]+)\]\s*$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Serialises codes as a .gct: magic, lines, terminator.
    /// </summary>
    /// <param name="lines">Code lines in order.</param>
    public static byte[] Build(IReadOnlyList<GeckoCodeLine> lines)
    {
        if (lines is null)
            throw new ArgumentNullException(nameof(lines));

        var bytes = new byte[Magic.Length + lines.Count * LineSize + Terminator.Length];
        Magic.CopyTo(bytes, 0);
        for (var i = 0; i < lines.Count; i++)
        {
            BigEndian.WriteUInt32(bytes, Magic.Length + i * LineSize, lines[i].Left);
            BigEndian.WriteUInt32(bytes, Magic.Length + i * LineSize + 4, lines[i].Right);
        }
        Terminator.CopyTo(bytes, bytes.Length - Terminator.Length);
        return bytes;
    }

    /// <summary>
    /// Reads a .gct, or a text file when it does not start with the magic.
    /// </summary>
    /// <param name="path">.gct, Ocarina .txt or Dolphin .ini.</param>
    /// <exception cref="InvalidDataException">No codes inside.</exception>
    public static IReadOnlyList<GeckoCodeLine> Load(string path)
    {
        if (path is null)
            throw new ArgumentNullException(nameof(path));

        var bytes = File.ReadAllBytes(path);
        return bytes.Length >= Magic.Length && bytes.Take(Magic.Length).SequenceEqual(Magic)
            ? Parse(bytes)
            : ParseText(System.Text.Encoding.UTF8.GetString(bytes));
    }

    /// <summary>
    /// Reads the lines of a .gct.
    /// </summary>
    /// <param name="bytes">Whole file.</param>
    /// <exception cref="InvalidDataException">Missing magic, or a length that is not whole lines.</exception>
    public static IReadOnlyList<GeckoCodeLine> Parse(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length < Magic.Length || !bytes.Take(Magic.Length).SequenceEqual(Magic))
            throw new InvalidDataException("Not a GCT: magic is missing.");
        if (bytes.Length % LineSize != 0)
            throw new InvalidDataException("GCT length is not a whole number of code lines.");

        var lines = new List<GeckoCodeLine>();
        for (var at = Magic.Length; at + LineSize <= bytes.Length; at += LineSize)
        {
            var line = new GeckoCodeLine(BigEndian.ReadUInt32(bytes, at), BigEndian.ReadUInt32(bytes, at + 4));
            if (line.Left == 0xF0000000 && line.Right == 0)
                break;
            lines.Add(line);
        }
        return lines;
    }

    /// <summary>
    /// Reads codes from Ocarina text (a game ID and name, then named blocks of hex pairs) or a Dolphin ini (hex pairs under [Gecko]); names, comments and anything else are skipped.
    /// </summary>
    /// <param name="text">File contents.</param>
    /// <exception cref="InvalidDataException">No code lines found.</exception>
    public static IReadOnlyList<GeckoCodeLine> ParseText(string text)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));

        var lines = new List<GeckoCodeLine>();
        var hasSections = false;
        var inGecko = false;
        foreach (var raw in text.Split('\n'))
        {
            var section = SectionLine.Match(raw);
            if (section.Success)
            {
                hasSections = true;
                inGecko = string.Equals(section.Groups["name"].Value.Trim(), "Gecko", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (hasSections && !inGecko)
                continue;
            var code = CodeLine.Match(raw);
            if (code.Success)
                lines.Add(new GeckoCodeLine(uint.Parse(code.Groups["left"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture), uint.Parse(code.Groups["right"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
        }
        if (lines.Count == 0)
            throw new InvalidDataException("No Gecko code lines found.");
        return lines;
    }
}
