# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- `WiiDisc`, `DiscHeader`, `PartitionTable`, `Partition`, `PartitionHeader`, `Ticket`: disc layout without touching data.
- `CommonKey`, `TitleKey.Derive`, `ClusterCipher`, `DiscCipher`: partition data crypto with a caller-supplied common key.
