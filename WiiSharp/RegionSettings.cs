namespace WiiSharp;

/// <summary>
/// The disc's region code and age-rating block.
/// </summary>
public sealed class RegionSettings
{
    /// <summary>
    /// Rating byte meaning the system is not used.
    /// </summary>
    public const byte NoRating = 0x80;
    /// <summary>
    /// Ratings block length.
    /// </summary>
    public const int RatingsSize = 16;
    /// <summary>
    /// Bytes <see cref="Parse"/> and <see cref="ToBytes"/> cover.
    /// </summary>
    public const int Size = 0x20;

    private readonly byte[] _ratings;

    /// <summary>
    /// Creates a new instance of the <see cref="RegionSettings"/> class.
    /// </summary>
    /// <param name="region">Region code.</param>
    /// <param name="ratings">Sixteen rating bytes; see <see cref="Rating"/> for the order.</param>
    /// <exception cref="ArgumentException">Not sixteen rating bytes.</exception>
    public RegionSettings(DiscRegion region, byte[] ratings)
    {
        if (ratings is null)
            throw new ArgumentNullException(nameof(ratings));
        if (ratings.Length != RatingsSize)
            throw new ArgumentException($"Ratings must be {RatingsSize} bytes.", nameof(ratings));

        Region = region;
        _ratings = (byte[])ratings.Clone();
    }

    /// <summary>
    /// Copy of the rating bytes.
    /// </summary>
    public byte[] Ratings => (byte[])_ratings.Clone();
    /// <summary>
    /// Region code.
    /// </summary>
    public DiscRegion Region { get; }

    /// <summary>
    /// The lowest rating in every system a region uses; what a region-changed disc gets.
    /// </summary>
    /// <param name="region">Target region.</param>
    /// <exception cref="ArgumentOutOfRangeException">No preset for the region.</exception>
    public static RegionSettings Preset(DiscRegion region)
    {
        var ratings = new byte[RatingsSize];
        for (var i = 0; i < ratings.Length; i++)
            ratings[i] = NoRating;

        switch (region)
        {
            case DiscRegion.Japan:
                ratings[(int)Rating.Cero] = 0x00;
                break;
            case DiscRegion.UnitedStates:
                ratings[(int)Rating.Esrb] = 0x06;
                break;
            case DiscRegion.Europe:
                ratings[(int)Rating.Usk] = 0x00;
                ratings[(int)Rating.Pegi] = 0x03;
                ratings[(int)Rating.PegiFinland] = 0x03;
                ratings[(int)Rating.PegiPortugal] = 0x04;
                ratings[(int)Rating.Bbfc] = 0x03;
                ratings[(int)Rating.Acb] = 0x00;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(region), region, "No preset for this region.");
        }
        return new RegionSettings(region, ratings);
    }

    /// <summary>
    /// Parses the region area.
    /// </summary>
    /// <param name="bytes">At least <see cref="Size"/> bytes.</param>
    public static RegionSettings Parse(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length < Size)
            throw new ArgumentException($"Need at least {Size} bytes.", nameof(bytes));

        var ratings = new byte[RatingsSize];
        Array.Copy(bytes, 0x10, ratings, 0, RatingsSize);
        return new RegionSettings((DiscRegion)BigEndian.ReadUInt32(bytes, 0), ratings);
    }

    /// <summary>
    /// The region area as stored on disc.
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[Size];
        BigEndian.WriteUInt32(bytes, 0, (uint)Region);
        _ratings.CopyTo(bytes, 0x10);
        return bytes;
    }

    /// <summary>
    /// Index of each rating system in the ratings block.
    /// </summary>
    public enum Rating
    {
        Cero = 0,
        Esrb = 1,
        Usk = 3,
        Pegi = 4,
        PegiFinland = 5,
        PegiPortugal = 6,
        Bbfc = 7,
        Acb = 8,
        Grb = 9,
    }
}
