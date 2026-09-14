namespace WiiSharp;

internal static class BigEndian
{
    public static ushort ReadUInt16(byte[] bytes, int offset) => (ushort)(bytes[offset] << 8 | bytes[offset + 1]);

    public static uint ReadUInt32(byte[] bytes, int offset) => (uint)(bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3]);

    public static ulong ReadUInt64(byte[] bytes, int offset) => (ulong)ReadUInt32(bytes, offset) << 32 | ReadUInt32(bytes, offset + 4);

    public static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        bytes[offset] = (byte)(value >> 8);
        bytes[offset + 1] = (byte)value;
    }

    public static void WriteUInt32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }

    public static void WriteUInt64(byte[] bytes, int offset, ulong value)
    {
        WriteUInt32(bytes, offset, (uint)(value >> 32));
        WriteUInt32(bytes, offset + 4, (uint)value);
    }
}
