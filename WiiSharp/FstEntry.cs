namespace WiiSharp;

/// <summary>
/// One file entry of a file system table as stored: its index, raw offset word and length.
/// </summary>
internal readonly record struct FstEntry(int Index, uint RawOffset, uint Length);
