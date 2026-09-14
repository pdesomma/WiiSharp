namespace WiiSharp;

/// <summary>
/// AES-128 key that unwraps title keys. Never shipped; supplied by the caller.
/// </summary>
public readonly struct CommonKey : IEquatable<CommonKey>
{
    private readonly byte[] _bytes;

    /// <summary>
    /// Creates a new instance of the <see cref="CommonKey"/> struct.
    /// </summary>
    /// <param name="bytes">Sixteen key bytes.</param>
    /// <exception cref="ArgumentException">Not sixteen bytes.</exception>
    public CommonKey(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length != DiscFormat.KeySize)
            throw new ArgumentException($"Key must be {DiscFormat.KeySize} bytes.", nameof(bytes));

        _bytes = (byte[])bytes.Clone();
    }

    /// <inheritdoc/>
    public bool Equals(CommonKey other) => ToArray().SequenceEqual(other.ToArray());

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CommonKey other && Equals(other);

    /// <summary>
    /// Reads a 16-byte key file.
    /// </summary>
    /// <param name="path">Key file path.</param>
    public static CommonKey FromFile(string path) => new(File.ReadAllBytes(path));

    /// <inheritdoc/>
    public override int GetHashCode() => BitConverter.ToInt32(ToArray(), 0);

    public static bool operator ==(CommonKey left, CommonKey right) => left.Equals(right);

    public static bool operator !=(CommonKey left, CommonKey right) => !left.Equals(right);

    /// <summary>
    /// Copy of the key bytes.
    /// </summary>
    public byte[] ToArray() => (byte[])(_bytes ?? new byte[DiscFormat.KeySize]).Clone();
}
