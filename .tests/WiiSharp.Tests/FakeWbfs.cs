namespace WiiSharp.Tests;

/// <summary>
/// Writes a WBFS container the way libwbfs lays it out, skipping all-zero disc sectors.
/// </summary>
internal static class FakeWbfs
{
    public const int HdShift = 9;
    public const int WbfsShift = 18;

    public static byte[] Build(params byte[][] discs)
    {
        var hdSize = 1 << HdShift;
        var wbfsSize = 1L << WbfsShift;
        var perDisc = WbfsFormat.WiiSectorsPerDisc >> (WbfsShift - WbfsFormat.WiiSectorShift);
        var infoSize = (WbfsFormat.DiscHeaderSize + perDisc * 2 + hdSize - 1) / hdSize * hdSize;

        var output = new MemoryStream();
        var header = new byte[hdSize];
        header[0] = 0x57;
        header[1] = 0x42;
        header[2] = 0x46;
        header[3] = 0x53;
        header[8] = HdShift;
        header[9] = WbfsShift;
        for (var d = 0; d < discs.Length; d++)
            header[WbfsFormat.HeaderSize + d] = 1;
        output.Write(header, 0, header.Length);

        var infos = new byte[discs.Length][];
        var data = new MemoryStream();
        ushort next = 1;
        for (var d = 0; d < discs.Length; d++)
        {
            infos[d] = new byte[infoSize];
            Array.Copy(discs[d], infos[d], Math.Min(WbfsFormat.DiscHeaderSize, discs[d].Length));
            var sectors = (int)((discs[d].Length + wbfsSize - 1) / wbfsSize);
            for (var s = 0; s < sectors; s++)
            {
                var chunk = new byte[wbfsSize];
                var length = (int)Math.Min(wbfsSize, discs[d].Length - s * wbfsSize);
                Array.Copy(discs[d], s * wbfsSize, chunk, 0, length);
                if (chunk.All(b => b == 0))
                    continue;
                infos[d][WbfsFormat.DiscHeaderSize + s * 2] = (byte)(next >> 8);
                infos[d][WbfsFormat.DiscHeaderSize + s * 2 + 1] = (byte)next;
                data.Write(chunk, 0, chunk.Length);
                next++;
            }
        }

        foreach (var info in infos)
            output.Write(info, 0, info.Length);
        output.SetLength(wbfsSize);
        output.Position = wbfsSize;
        data.WriteTo(output);

        var bytes = output.ToArray();
        var hdSectors = (uint)(bytes.Length / hdSize);
        bytes[4] = (byte)(hdSectors >> 24);
        bytes[5] = (byte)(hdSectors >> 16);
        bytes[6] = (byte)(hdSectors >> 8);
        bytes[7] = (byte)hdSectors;
        return bytes;
    }

    public static string Write(string directory, byte[] wbfs, long splitAt = long.MaxValue)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "game.wbfs");
        for (var part = 0; part * splitAt < wbfs.Length; part++)
        {
            var length = (int)Math.Min(splitAt, wbfs.Length - part * splitAt);
            File.WriteAllBytes(WbfsFormat.PartPath(path, part), wbfs.Skip((int)(part * splitAt)).Take(length).ToArray());
        }
        return path;
    }
}
