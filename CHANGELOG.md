# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.6.1] - 2026-09-15

### Fixed
- `HashGroup`: the H2 table sits at 0x340 of a cluster, not 0x320; discs built with earlier versions fail the console's hash check and hang.

## [0.6.0] - 2026-09-15

### Added
- `NkitGameCube`: compact a GameCube image to NKit and restore it bit for bit; output matches NKit's own files byte for byte, CRC-32 patched so the compact file checksums like its source.
- `NkitHeader`: the block NKit keeps at 0x200.
- `JunkGenerator`: the lagged Fibonacci filler between files, reseeded per 32 KiB sector.
- `Crc32`: reflected CRC-32 with rewind and a four-byte forcing patch.
- `PartitionDataStream` and `PartitionSystemFiles.Read` take `hashed: false` to read partitions NKit stored bare, without hash blocks.

## [0.5.0] - 2026-09-14

### Added
- `DolHeader`, `DolSection`: parse a DOL header; sections, BSS, entry point and the file length it implies.
- `WadHeader`, `WadFile`: read a WAD's header, ticket and TMD without touching the contents.

## [0.4.0] - 2026-09-14

### Added
- `WiiDiscBuilder`, `DiscFile`, `WiiDiscBuildResult`: write a single-partition disc with plaintext, fully hashed clusters (H0-H3), a fakesigned ticket and TMD, the region area and the retail disc magic.
- `PartitionSystemFiles`, `Apploader`, `PartitionDataStream`: pull boot.bin, bi2.bin, the apploader and the certificate chain out of a plaintext partition, and read its payload with hash blocks skipped.
- `Fst`, `FstFile`: build and parse the file system table.
- `Tmd`, `TmdContent`, `Ticket.Build`, `Signature`: build disc tickets and TMDs; trucha-style fakesigning.
- `HashGroup`: H0-H2 tables for one 64-cluster group.
- `GczFile`, `GczHeader`, `GczStream`: read Dolphin GCZ images as a seekable stream.
- `DiscFormat`: hash group, H3, apploader and single-layer constants.

## [0.3.0] - 2026-09-14

### Added
- `WbfsFile`, `WbfsHeader`, `WbfsDisc`, `WbfsFormat`: read WBFS containers, including .wbs1/.wbs2 split parts, and expose each disc as a seekable image stream.

## [0.2.0] - 2026-09-14

### Added
- `DiscRegion`, `RegionSettings` and `RegionArea`: read and rewrite the region code and age ratings at 0x4E000; `RegionSettings.Preset` gives the lowest rating per region.

## [0.1.0] - 2026-09-14

### Added
- `WiiDisc`, `DiscHeader`, `PartitionTable`, `Partition`, `PartitionHeader`, `Ticket`: disc layout without touching data.
- `CommonKey`, `TitleKey.Derive`, `ClusterCipher`, `DiscCipher`: partition data crypto with a caller-supplied common key.
