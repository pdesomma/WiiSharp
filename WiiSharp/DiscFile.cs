namespace WiiSharp;

/// <summary>
/// A file to place on a disc being built.
/// </summary>
public sealed class DiscFile
{
    /// <summary>
    /// Creates a new instance of the <see cref="DiscFile"/> class.
    /// </summary>
    /// <param name="path">Slash-separated path from the partition root.</param>
    /// <param name="content">Seekable source; read during the build, not disposed.</param>
    public DiscFile(string path, Stream content)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("Path must not be empty.", nameof(path));
        if (content is null)
            throw new ArgumentNullException(nameof(content));
        if (!content.CanSeek || !content.CanRead)
            throw new ArgumentException("Content must be readable and seekable.", nameof(content));

        Path = path;
        Content = content;
    }

    /// <summary>
    /// Source bytes.
    /// </summary>
    public Stream Content { get; }
    /// <summary>
    /// Bytes the file occupies.
    /// </summary>
    public long Length => Content.Length;
    /// <summary>
    /// Path from the partition root.
    /// </summary>
    public string Path { get; }
}
