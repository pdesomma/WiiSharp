namespace WiiSharp;

/// <summary>
/// Absolute byte range on a disc.
/// </summary>
/// <param name="Start">First byte.</param>
/// <param name="End">One past the last byte.</param>
public readonly record struct DataRange(long Start, long End)
{
    /// <summary>
    /// Bytes covered.
    /// </summary>
    public long Length => End - Start;
}
