using System.Security.Cryptography;

namespace WiiSharp;

/// <summary>
/// Encrypts and decrypts one 0x8000-byte cluster: hashes with a zero IV, then data with an IV taken from the encrypted hashes.
/// </summary>
public sealed class ClusterCipher : IDisposable
{
    private readonly Aes _aes;
    private readonly byte[] _key;

    /// <summary>
    /// Creates a new instance of the <see cref="ClusterCipher"/> class.
    /// </summary>
    /// <param name="titleKey">Partition's title key.</param>
    public ClusterCipher(TitleKey titleKey)
    {
        _key = titleKey.ToArray();
        _aes = Aes.Create();
        _aes.Mode = CipherMode.CBC;
        _aes.Padding = PaddingMode.None;
        _aes.KeySize = 128;
        _aes.BlockSize = 128;
        _aes.Key = _key;
    }

    /// <summary>
    /// Decrypts a cluster in place.
    /// </summary>
    /// <param name="cluster">Exactly <see cref="DiscFormat.ClusterSize"/> bytes.</param>
    public void Decrypt(byte[] cluster)
    {
        Check(cluster);
        var iv = new byte[DiscFormat.KeySize];
        Array.Copy(cluster, DiscFormat.ClusterIvOffset, iv, 0, DiscFormat.KeySize);
        Transform(cluster, 0, DiscFormat.ClusterHashSize, new byte[DiscFormat.KeySize], encrypt: false);
        Transform(cluster, DiscFormat.ClusterHashSize, DiscFormat.ClusterDataSize, iv, encrypt: false);
    }

    /// <inheritdoc/>
    public void Dispose() => _aes.Dispose();

    /// <summary>
    /// Encrypts a cluster in place.
    /// </summary>
    /// <param name="cluster">Exactly <see cref="DiscFormat.ClusterSize"/> bytes.</param>
    public void Encrypt(byte[] cluster)
    {
        Check(cluster);
        Transform(cluster, 0, DiscFormat.ClusterHashSize, new byte[DiscFormat.KeySize], encrypt: true);
        var iv = new byte[DiscFormat.KeySize];
        Array.Copy(cluster, DiscFormat.ClusterIvOffset, iv, 0, DiscFormat.KeySize);
        Transform(cluster, DiscFormat.ClusterHashSize, DiscFormat.ClusterDataSize, iv, encrypt: true);
    }

    private static void Check(byte[] cluster)
    {
        if (cluster is null)
            throw new ArgumentNullException(nameof(cluster));
        if (cluster.Length != DiscFormat.ClusterSize)
            throw new ArgumentException($"A cluster is {DiscFormat.ClusterSize} bytes.", nameof(cluster));
    }

    private void Transform(byte[] buffer, int offset, int count, byte[] iv, bool encrypt)
    {
#if NET6_0_OR_GREATER
        var input = new ReadOnlySpan<byte>(buffer, offset, count);
        var output = new Span<byte>(buffer, offset, count);
        if (encrypt)
            _aes.EncryptCbc(input, iv, output, PaddingMode.None);
        else
            _aes.DecryptCbc(input, iv, output, PaddingMode.None);
#else
        using var transform = encrypt ? _aes.CreateEncryptor(_key, iv) : _aes.CreateDecryptor(_key, iv);
        transform.TransformBlock(buffer, offset, count, buffer, offset);
#endif
    }
}
