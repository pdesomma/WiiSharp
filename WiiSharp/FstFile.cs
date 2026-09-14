namespace WiiSharp;

/// <summary>
/// One file listed by the file system table.
/// </summary>
/// <param name="Path">Slash-separated path from the root, e.g. sound/bgm.brstm.</param>
/// <param name="Offset">Offset within partition data.</param>
/// <param name="Length">Bytes.</param>
public readonly record struct FstFile(string Path, long Offset, long Length);
