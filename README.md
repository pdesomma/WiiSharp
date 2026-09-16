# WiiSharp

.NET library for Wii and GameCube file formats.

Not affiliated with or endorsed by Nintendo. Wii and GameCube are trademarks of Nintendo.

Disc layout and partition crypto follow the [WiiBrew](https://wiibrew.org/wiki/Wii_disc) documentation and the behaviour of [nfs2iso2nfs](https://github.com/FIX94/nfs2iso2nfs) by sabykos, piratesephiroth and FIX94. The WBFS container follows libwbfs as used by [wit](https://wit.wiimm.de/); the disc builder lays files out the way wit does. Ticket and TMD fields follow WiiBrew and the carrier-disc notes in [haydenmc/wiivci](https://github.com/haydenmc/wiivci). The GCZ format follows Dolphin's `CompressedBlob`.

## Status

Early. What exists: the disc header, partition table, tickets, title-key derivation from a caller-supplied common key, per-cluster partition data crypto — enough to implement `WiiUSharp.Nfs.IPartitionCipher` — a reader for WBFS containers, split parts included, a reader for Dolphin GCZ images, a disc builder that writes a hashed plaintext partition around any main.dol and files, reusing the apploader and certificate chain of a disc you already have, readers for DOL headers and the signed parts of a WAD, a DOL editor that adds sections and patches by address, Gecko cheat codes in .gct and text form, the junk generator Nintendo's mastering used between files, and NKit: GameCube images compact to `.nkit.iso` and restore bit for bit, Wii NKit partitions read as bare plaintext. WAD contents and DOL patching follow.

```csharp
using WiiSharp;

var commonKey = CommonKey.FromFile("common-key.bin");   // never shipped with this library

using var iso = File.OpenRead("game.iso");
WiiDisc disc = WiiDisc.Read(iso);                       // header + partitions, no data touched
Partition game = disc.DataPartitions[0];
TitleKey key = TitleKey.Derive(commonKey, game.Ticket);

using var plain = File.Create("game.plain.iso");
DataRange range = new DiscCipher(commonKey).Decrypt(iso, plain);   // data partitions decrypted, rest copied

using var wbfs = WbfsFile.Open("game.wbfs");             // picks up game.wbs1, game.wbs2 ... beside it
using Stream image = wbfs.Discs[0].OpenStream();          // seekable ISO view; unstored sectors read as zeros

using var gcz = GczFile.Open("game.gcz");                 // Dolphin compressed image
using Stream gc = gcz.OpenStream();                       // seekable decompressed view

PartitionSystemFiles system = PartitionSystemFiles.Read(plain, disc.DataPartitions[0]);   // apploader, boot/bi2, certs
var builder = new WiiDiscBuilder("GALE01", "Melee", system, File.ReadAllBytes("forwarder.dol"))
{
    Region = RegionArea.Read(plain),
};
builder.Files.Add(new DiscFile("game.iso", gc));
using var built = File.Create("carrier.iso");
WiiDiscBuildResult result = builder.Build(built);         // plaintext clusters with H0-H3, fakesigned ticket + TMD

using var compact = File.Create("game.nkit.iso");
NkitHeader nkit = NkitGameCube.Compact(gc, compact);     // junk between files becomes records; boots as-is in Nintendont
NkitGameCube.Restore(compact, File.Create("back.iso"));  // bit-exact, checked against the recorded CRC-32
```

## Packages

| Package | Targets | Purpose |
|---|---|---|
| `WiiSharp` | net48, net6.0, net8.0, net10.0 | Disc images, partitions, tickets, TMDs, title keys, disc building, DOL and WAD headers, WBFS and GCZ containers. |

```
dotnet add package WiiSharp
```

## Building

```
dotnet build WiiSharp.sln
dotnet test WiiSharp.sln
```

Requires the .NET 10 SDK (`global.json`). `dotnet pack -c Release` produces the NuGet package.

## License

MIT — see [LICENSE](LICENSE).
