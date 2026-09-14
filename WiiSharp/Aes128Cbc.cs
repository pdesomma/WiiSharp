using System.Security.Cryptography;

namespace WiiSharp;

internal static class Aes128Cbc
{
    public static byte[] Transform(byte[] key, byte[] iv, byte[] data, bool encrypt)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.KeySize = 128;
        aes.BlockSize = 128;
        aes.Key = key;
#if NET6_0_OR_GREATER
        return encrypt ? aes.EncryptCbc(data, iv, PaddingMode.None) : aes.DecryptCbc(data, iv, PaddingMode.None);
#else
        using var transform = encrypt ? aes.CreateEncryptor(key, iv) : aes.CreateDecryptor(key, iv);
        return transform.TransformFinalBlock(data, 0, data.Length);
#endif
    }
}
