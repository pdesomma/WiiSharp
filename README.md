# WiiSharp

.NET library for Wii and GameCube file formats. No native dependencies, no keys, no Nintendo assets.

Not affiliated with or endorsed by Nintendo. Wii and GameCube are trademarks of Nintendo.

## Status

Early. The first target is Wii disc images: the partition table, tickets, title-key derivation from a caller-supplied common key, and per-cluster partition data crypto — enough to implement `WiiUSharp.Nfs.IPartitionCipher` and replace `wit`/`nfs2iso2nfs` in a Wii U injector. GameCube discs, WAD files and DOL patching follow.

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

Requires the .NET 10 SDK (`global.json`). `dotnet build -c Release` also produces the NuGet package.

## License

MIT — see [LICENSE](LICENSE).
