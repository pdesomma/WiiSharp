using System.Text;

namespace WiiSharp;

/// <summary>
/// The Wii file system table: 12-byte entries followed by a string table, file offsets stored shifted right by 2.
/// </summary>
public static class Fst
{
    /// <summary>
    /// Bytes per entry.
    /// </summary>
    public const int EntrySize = 12;

    /// <summary>
    /// Serializes a table whose directories are implied by the paths; entries keep the given order within each directory.
    /// </summary>
    /// <param name="files">Files with their partition-data offsets.</param>
    public static byte[] Build(IReadOnlyList<FstFile> files)
    {
        if (files is null)
            throw new ArgumentNullException(nameof(files));

        var root = new Node(string.Empty);
        foreach (var file in files)
        {
            if (string.IsNullOrEmpty(file.Path))
                throw new ArgumentException("File paths must not be empty.", nameof(files));
            if (file.Offset < 0 || file.Offset % 4 != 0)
                throw new ArgumentException($"{file.Path}: offsets must be non-negative multiples of 4.", nameof(files));
            if (file.Length < 0 || file.Length > uint.MaxValue)
                throw new ArgumentException($"{file.Path}: length does not fit the table.", nameof(files));

            var parts = file.Path.Split('/');
            var node = root;
            for (var i = 0; i < parts.Length - 1; i++)
                node = node.Directory(parts[i]);
            node.Children.Add(new Node(parts[parts.Length - 1]) { File = file });
        }

        var entries = new List<byte[]>();
        var names = new MemoryStream();
        Emit(root, 0, entries, names);
        var bytes = new byte[entries.Count * EntrySize + names.Length];
        for (var i = 0; i < entries.Count; i++)
            entries[i].CopyTo(bytes, i * EntrySize);
        names.ToArray().CopyTo(bytes, entries.Count * EntrySize);
        return bytes;
    }

    /// <summary>
    /// Lists every file with its full path.
    /// </summary>
    /// <param name="bytes">Whole table.</param>
    public static IReadOnlyList<FstFile> Parse(byte[] bytes)
    {
        if (bytes is null)
            throw new ArgumentNullException(nameof(bytes));
        if (bytes.Length < EntrySize)
            throw new InvalidDataException("Table is shorter than its root entry.");

        var count = checked((int)BigEndian.ReadUInt32(bytes, 8));
        if (count < 1 || count * EntrySize > bytes.Length)
            throw new InvalidDataException("Entry count exceeds the table.");

        var stringTable = count * EntrySize;
        var files = new List<FstFile>();
        var directories = new Stack<(int End, string Path)>();
        directories.Push((count, string.Empty));
        for (var i = 1; i < count; i++)
        {
            while (directories.Peek().End <= i)
                directories.Pop();

            var at = i * EntrySize;
            var isDirectory = bytes[at] != 0;
            var nameOffset = (bytes[at + 1] << 16) | (bytes[at + 2] << 8) | bytes[at + 3];
            var name = ReadName(bytes, stringTable + nameOffset);
            var path = directories.Peek().Path.Length == 0 ? name : directories.Peek().Path + "/" + name;
            if (isDirectory)
            {
                var end = checked((int)BigEndian.ReadUInt32(bytes, at + 8));
                if (end < i + 1 || end > count)
                    throw new InvalidDataException($"Directory {path} has an invalid extent.");
                directories.Push((end, path));
            }
            else
            {
                files.Add(new FstFile(path, (long)BigEndian.ReadUInt32(bytes, at + 4) << 2, BigEndian.ReadUInt32(bytes, at + 8)));
            }
        }
        return files;
    }

    /// <summary>
    /// File entries as stored: index, raw offset word and length; directories skipped.
    /// </summary>
    /// <param name="bytes">Whole table.</param>
    internal static List<FstEntry> FileEntries(byte[] bytes)
    {
        if (bytes.Length < EntrySize)
            throw new InvalidDataException("Table is shorter than its root entry.");

        var count = checked((int)BigEndian.ReadUInt32(bytes, 8));
        if (count < 1 || count * EntrySize > bytes.Length)
            throw new InvalidDataException("Entry count exceeds the table.");

        var entries = new List<FstEntry>();
        for (var i = 1; i < count; i++)
        {
            var at = i * EntrySize;
            if (bytes[at] == 0)
                entries.Add(new FstEntry(i, BigEndian.ReadUInt32(bytes, at + 4), BigEndian.ReadUInt32(bytes, at + 8)));
        }
        return entries;
    }

    /// <summary>
    /// Overwrites one file entry's raw offset word and length.
    /// </summary>
    /// <param name="bytes">Whole table.</param>
    /// <param name="index">Entry index.</param>
    /// <param name="rawOffset">Offset word as stored.</param>
    /// <param name="length">File length.</param>
    internal static void SetFileEntry(byte[] bytes, int index, uint rawOffset, uint length)
    {
        BigEndian.WriteUInt32(bytes, index * EntrySize + 4, rawOffset);
        BigEndian.WriteUInt32(bytes, index * EntrySize + 8, length);
    }

    private static void Emit(Node node, int parent, List<byte[]> entries, MemoryStream names)
    {
        var entry = new byte[EntrySize];
        var index = entries.Count;
        entries.Add(entry);
        var nameOffset = (int)names.Length;
        var name = Encoding.ASCII.GetBytes(node.Name);
        names.Write(name, 0, name.Length);
        names.WriteByte(0);
        entry[1] = (byte)(nameOffset >> 16);
        entry[2] = (byte)(nameOffset >> 8);
        entry[3] = (byte)nameOffset;

        if (node.File is { } file)
        {
            BigEndian.WriteUInt32(entry, 4, (uint)(file.Offset >> 2));
            BigEndian.WriteUInt32(entry, 8, (uint)file.Length);
            return;
        }

        entry[0] = 1;
        BigEndian.WriteUInt32(entry, 4, (uint)parent);
        foreach (var child in node.Children)
            Emit(child, index, entries, names);
        BigEndian.WriteUInt32(entry, 8, (uint)entries.Count);
    }

    private static string ReadName(byte[] bytes, int at)
    {
        var end = at;
        while (end < bytes.Length && bytes[end] != 0)
            end++;
        if (end >= bytes.Length)
            throw new InvalidDataException("Unterminated name in the string table.");
        return Encoding.ASCII.GetString(bytes, at, end - at);
    }

    private sealed class Node
    {
        public Node(string name)
        {
            Name = name;
        }

        public List<Node> Children { get; } = new();
        public FstFile? File { get; init; }
        public string Name { get; }

        public Node Directory(string name)
        {
            var existing = Children.FirstOrDefault(c => c.File is null && c.Name == name);
            if (existing is not null)
                return existing;
            var created = new Node(name);
            Children.Add(created);
            return created;
        }
    }
}
