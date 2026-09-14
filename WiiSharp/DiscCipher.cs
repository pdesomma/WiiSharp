namespace WiiSharp;

/// <summary>
/// Copies a disc image while decrypting or encrypting every data partition's clusters. Everything else passes through unchanged.
/// </summary>
public sealed class DiscCipher
{
    private readonly CommonKey _commonKey;

    /// <summary>
    /// Creates a new instance of the <see cref="DiscCipher"/> class.
    /// </summary>
    /// <param name="commonKey">Key that unwraps each partition's title key.</param>
    public DiscCipher(CommonKey commonKey)
    {
        _commonKey = commonKey;
    }

    /// <summary>
    /// Copies an encrypted disc to one whose data partitions are plaintext.
    /// </summary>
    /// <param name="disc">Seekable encrypted image.</param>
    /// <param name="output">Destination.</param>
    /// <param name="cancellationToken">Cancels between clusters.</param>
    /// <returns>Range spanning all data partitions.</returns>
    public DataRange Decrypt(Stream disc, Stream output, CancellationToken cancellationToken = default) =>
        Transcode(disc, output, encrypt: false, cancellationToken);

    /// <summary>
    /// Copies a disc whose data partitions are plaintext back to encrypted form.
    /// </summary>
    /// <param name="disc">Seekable plaintext image.</param>
    /// <param name="output">Destination.</param>
    /// <param name="cancellationToken">Cancels between clusters.</param>
    /// <returns>Range spanning all data partitions.</returns>
    public DataRange Encrypt(Stream disc, Stream output, CancellationToken cancellationToken = default) =>
        Transcode(disc, output, encrypt: true, cancellationToken);

    private DataRange Transcode(Stream disc, Stream output, bool encrypt, CancellationToken cancellationToken)
    {
        if (output is null)
            throw new ArgumentNullException(nameof(output));

        var layout = WiiDisc.Read(disc);
        var partitions = layout.DataPartitions;
        if (partitions.Count == 0)
            throw new InvalidDataException("Disc has no data partition.");

        var buffer = new byte[DiscFormat.ClusterSize];
        disc.Position = 0;
        long position = 0;
        foreach (var partition in partitions)
        {
            if (partition.DataStart < position)
                throw new InvalidDataException("Data partitions overlap.");

            disc.CopyExactly(output, partition.DataStart - position, buffer, cancellationToken);
            using var cipher = new ClusterCipher(TitleKey.Derive(_commonKey, partition.Ticket));
            var clusters = partition.Header.DataSize / DiscFormat.ClusterSize;
            for (long i = 0; i < clusters; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                disc.ReadExactly(buffer, 0, buffer.Length);
                if (encrypt)
                    cipher.Encrypt(buffer);
                else
                    cipher.Decrypt(buffer);
                output.Write(buffer, 0, buffer.Length);
            }

            var tail = partition.Header.DataSize % DiscFormat.ClusterSize;
            disc.CopyExactly(output, tail, buffer, cancellationToken);
            position = partition.DataEnd;
        }

        int read;
        while ((read = disc.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.Write(buffer, 0, read);
        }

        return new DataRange(partitions[0].Offset, partitions[partitions.Count - 1].DataEnd);
    }
}
