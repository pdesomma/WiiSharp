# WiiSharp

.NET library for Wii and GameCube file formats.

Not affiliated with or endorsed by Nintendo. Wii and GameCube are trademarks of Nintendo.

Disc layout and partition crypto follow the [WiiBrew](https://wiibrew.org/wiki/Wii_disc) documentation and the behaviour of [nfs2iso2nfs](https://github.com/FIX94/nfs2iso2nfs) by sabykos, piratesephiroth and FIX94. The WBFS container follows libwbfs as used by [wit](https://wit.wiimm.de/).

## Status

Early. What exists: the disc header, partition table, tickets, title-key derivation from a caller-supplied common key, per-cluster partition data crypto — enough to implement `WiiUSharp.Nfs.IPartitionCipher` — and a reader for WBFS containers, split parts included. GameCube discs, WAD files and DOL patching follow.

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
```

## Packages

| Package | Targets | Purpose |
|---|---|---|
| `WiiSharp` | net48, net6.0, net8.0, net10.0 | Disc images, partitions, tickets, title keys, WBFS containers. |

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
