namespace WiiSharp;

/// <summary>
/// One loadable section of a DOL.
/// </summary>
/// <param name="Offset">File offset.</param>
/// <param name="Address">Load address.</param>
/// <param name="Size">Bytes.</param>
public readonly record struct DolSection(uint Offset, uint Address, uint Size)
{
    /// <summary>
    /// One past the last file byte.
    /// </summary>
    public long End => Offset + (long)Size;
}
