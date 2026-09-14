namespace WiiSharp;

/// <summary>
/// One content record of a TMD.
/// </summary>
/// <param name="Id">Content ID.</param>
/// <param name="Index">Position in the TMD.</param>
/// <param name="Type">Content type flags.</param>
/// <param name="Size">Bytes.</param>
/// <param name="Hash">SHA-1 of the content.</param>
public sealed record TmdContent(uint Id, ushort Index, ushort Type, ulong Size, byte[] Hash);
