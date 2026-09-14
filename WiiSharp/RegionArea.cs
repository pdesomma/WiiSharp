namespace WiiSharp;

/// <summary>
/// The unencrypted region area at 0x4E000 of a disc image.
/// </summary>
public static class RegionArea
{
    /// <summary>
    /// Absolute offset on the disc.
    /// </summary>
    public const long Offset = 0x4E000;

    /// <summary>
    /// Reads the region settings.
    /// </summary>
    /// <param name="disc">Seekable disc image.</param>
    public static RegionSettings Read(Stream disc)
    {
        if (disc is null)
            throw new ArgumentNullException(nameof(disc));

        return RegionSettings.Parse(disc.ReadExactlyAt(Offset, RegionSettings.Size));
    }

    /// <summary>
    /// Overwrites the region settings in place.
    /// </summary>
    /// <param name="disc">Seekable, writable disc image.</param>
    /// <param name="settings">Settings to store.</param>
    public static void Write(Stream disc, RegionSettings settings)
    {
        if (disc is null)
            throw new ArgumentNullException(nameof(disc));
        if (settings is null)
            throw new ArgumentNullException(nameof(settings));

        var bytes = settings.ToBytes();
        disc.Position = Offset;
        disc.Write(bytes, 0, bytes.Length);
    }
}
