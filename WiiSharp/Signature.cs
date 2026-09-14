using System.Security.Cryptography;
using System.Text;

namespace WiiSharp;

/// <summary>
/// The signed-blob preamble shared by tickets and TMDs: type, signature, padding, issuer.
/// </summary>
public static class Signature
{
    /// <summary>
    /// Offset of the 64-byte issuer, where the signed body begins.
    /// </summary>
    public const int BodyOffset = 0x140;
    /// <summary>
    /// Signature type of RSA-2048 with SHA-1.
    /// </summary>
    public const uint Rsa2048Type = 0x00010001;

    /// <summary>
    /// Sets a 16-bit filler so the SHA-1 of the body starts with a zero byte, which the trucha-style check accepts.
    /// </summary>
    /// <param name="blob">Whole ticket or TMD; the signature bytes are left zero.</param>
    /// <param name="fillerOffset">Offset of an unused 16-bit field inside the body.</param>
    /// <exception cref="InvalidOperationException">No filler value produced a leading zero.</exception>
    public static void FakeSign(byte[] blob, int fillerOffset)
    {
        if (blob is null)
            throw new ArgumentNullException(nameof(blob));
        if (fillerOffset < BodyOffset || fillerOffset + 2 > blob.Length)
            throw new ArgumentOutOfRangeException(nameof(fillerOffset));

        using var sha1 = SHA1.Create();
        for (var filler = 0; filler <= ushort.MaxValue; filler++)
        {
            BigEndian.WriteUInt16(blob, fillerOffset, (ushort)filler);
            if (sha1.ComputeHash(blob, BodyOffset, blob.Length - BodyOffset)[0] == 0)
                return;
        }
        throw new InvalidOperationException("No filler value yields a leading zero hash.");
    }

    /// <summary>
    /// True when the SHA-1 of the body starts with a zero byte.
    /// </summary>
    /// <param name="blob">Whole ticket or TMD.</param>
    public static bool IsFakeSigned(byte[] blob)
    {
        if (blob is null)
            throw new ArgumentNullException(nameof(blob));

        using var sha1 = SHA1.Create();
        return sha1.ComputeHash(blob, BodyOffset, blob.Length - BodyOffset)[0] == 0;
    }

    /// <summary>
    /// Issuer string stored at <see cref="BodyOffset"/>.
    /// </summary>
    /// <param name="blob">Whole ticket or TMD.</param>
    public static string ReadIssuer(byte[] blob)
    {
        if (blob is null)
            throw new ArgumentNullException(nameof(blob));

        return Encoding.ASCII.GetString(blob, BodyOffset, 0x40).TrimEnd('\0');
    }

    internal static void WriteIssuer(byte[] blob, string issuer)
    {
        var bytes = Encoding.ASCII.GetBytes(issuer);
        Array.Clear(blob, BodyOffset, 0x40);
        bytes.CopyTo(blob, BodyOffset);
    }
}
