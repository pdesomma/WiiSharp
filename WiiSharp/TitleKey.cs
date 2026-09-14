using System.Security.Cryptography;

namespace WiiSharp;

/// <summary>
/// AES-128 key that encrypts one partition's data.
/// </summary>
public readonly struct TitleKey : IEquatable<TitleKey>
{
    private readonly byte[] _bytes;

    /// <summary>
    /// Creates a new instance of the <see cref="TitleKey"/> struct.
    /// </summary>
    /// <param name="bytes">Sixteen key bytes.</param>
    /// <exception cref="ArgumentException">Not sixteen bytes.</exception>
    public TitleKey(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length != DiscFormat.KeySize)
            throw new ArgumentException($"Key must be {DiscFormat.KeySize} bytes.", nameof(bytes));

        _bytes = (byte[])bytes.Clone();
    }

    /// <summary>
    /// Unwraps the ticket's title key: AES-CBC with the common key and the title ID as IV.
    /// </summary>
    /// <param name="commonKey">Key matching the ticket's <see cref="Ticket.CommonKeyIndex"/>.</param>
    /// <param name="ticket">Ticket holding the encrypted key.</param>
    public static TitleKey Derive(CommonKey commonKey, Ticket ticket)
    {
        if (ticket is null)
            throw new ArgumentNullException(nameof(ticket));

        var iv = new byte[DiscFormat.KeySize];
        ticket.TitleId.CopyTo(iv, 0);
        return new TitleKey(Aes128Cbc.Transform(commonKey.ToArray(), iv, ticket.EncryptedTitleKey, encrypt: false));
    }

    /// <inheritdoc/>
    public bool Equals(TitleKey other) => ToArray().SequenceEqual(other.ToArray());

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TitleKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => BitConverter.ToInt32(ToArray(), 0);

    public static bool operator ==(TitleKey left, TitleKey right) => left.Equals(right);

    public static bool operator !=(TitleKey left, TitleKey right) => !left.Equals(right);

    /// <summary>
    /// Copy of the key bytes.
    /// </summary>
    public byte[] ToArray() => (byte[])(_bytes ?? new byte[DiscFormat.KeySize]).Clone();
}
