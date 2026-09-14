# WiiSharp

.NET library for Wii and GameCube file formats.

Not affiliated with or endorsed by Nintendo. Wii and GameCube are trademarks of Nintendo.

Disc layout and partition crypto follow the [WiiBrew](https://wiibrew.org/wiki/Wii_disc) documentation and the behaviour of [nfs2iso2nfs](https://github.com/FIX94/nfs2iso2nfs) by sabykos, piratesephiroth and FIX94. 

## Status

Early. What exists: the disc header, partition table, tickets, title-key derivation from a caller-supplied common key, and per-cluster partition data crypto — enough to implement `WiiUSharp.Nfs.IPartitionCipher`. GameCube discs, WAD files and DOL patching follow.

```csharp
using WiiSharp;

var commonKey = CommonKey.FromFile("common-key.bin");   // never shipped with this library

using var iso = File.OpenRead("game.iso");
WiiDisc disc = WiiDisc.Read(iso);                       // header + partitions, no data touched
Partition game = disc.DataPartitions[0];
TitleKey key = TitleKey.Derive(commonKey, game.Ticket);

using var plain = File.Create("game.plain.iso");
DataRange range = new DiscCipher(commonKey).Decrypt(iso, plain);   // data partitions decrypted, rest copied
```

## Packages

| Package | Targets | Purpose |
|---|---|---|
| `WiiSharp` | net48, net6.0, net8.0, net10.0 | Disc images, partitions, tickets, title keys. |

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
