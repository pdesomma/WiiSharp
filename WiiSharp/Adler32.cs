namespace WiiSharp;

internal static class Adler32
{
    private const uint Modulus = 65521;

    public static uint Compute(byte[] bytes, int offset, int count)
    {
        uint a = 1, b = 0;
        for (var i = 0; i < count; i++)
        {
            a = (a + bytes[offset + i]) % Modulus;
            b = (b + a) % Modulus;
        }
        return b << 16 | a;
    }
}
