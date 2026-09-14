# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.2.0] - 2026-09-14

### Added
- `DiscRegion`, `RegionSettings` and `RegionArea`: read and rewrite the region code and age ratings at 0x4E000; `RegionSettings.Preset` gives the lowest rating per region.

## [0.1.0] - 2026-09-14

### Added
- `WiiDisc`, `DiscHeader`, `PartitionTable`, `Partition`, `PartitionHeader`, `Ticket`: disc layout without touching data.
- `CommonKey`, `TitleKey.Derive`, `ClusterCipher`, `DiscCipher`: partition data crypto with a caller-supplied common key.
