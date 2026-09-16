namespace WiiSharp;

/// <summary>
/// One line of a Gecko code: the two 32-bit words a .gct stores back to back.
/// </summary>
/// <param name="Left">First word: code type and address.</param>
/// <param name="Right">Second word: value or count.</param>
public readonly record struct GeckoCodeLine(uint Left, uint Right);
