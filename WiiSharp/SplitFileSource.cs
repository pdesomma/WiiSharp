namespace WiiSharp;

/// <summary>
/// Random-access reads over files laid end to end.
/// </summary>
internal sealed class SplitFileSource : IDisposable
{
    private readonly FileStream[] _files;
    private readonly long[] _starts;

    public SplitFileSource(IReadOnlyList<string> paths)
    {
        _files = new FileStream[paths.Count];
        _starts = new long[paths.Count + 1];
        for (var i = 0; i < paths.Count; i++)
        {
            _files[i] = new FileStream(paths[i], FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
            _starts[i + 1] = _starts[i] + _files[i].Length;
        }
    }

    public long Length => _starts[_starts.Length - 1];

    public void Dispose()
    {
        foreach (var file in _files)
            file.Dispose();
    }

    /// <summary>
    /// Reads up to count bytes at an absolute offset; fewer only at the very end.
    /// </summary>
    public int ReadAt(long offset, byte[] buffer, int index, int count)
    {
        var total = 0;
        while (count > 0 && offset < Length)
        {
            var part = Locate(offset);
            var file = _files[part];
            file.Position = offset - _starts[part];
            var read = file.Read(buffer, index, (int)Math.Min(count, _starts[part + 1] - offset));
            if (read == 0)
                break;
            offset += read;
            index += read;
            count -= read;
            total += read;
        }
        return total;
    }

    private int Locate(long offset)
    {
        var part = 0;
        while (part + 1 < _files.Length && offset >= _starts[part + 1])
            part++;
        return part;
    }
}
