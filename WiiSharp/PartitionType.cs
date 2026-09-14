namespace WiiSharp;

/// <summary>
/// Kind of partition as listed in the partition table.
/// </summary>
public enum PartitionType : uint
{
    /// <summary>
    /// Game data.
    /// </summary>
    Data = 0,
    /// <summary>
    /// System update.
    /// </summary>
    Update = 1,
    /// <summary>
    /// Channel installer.
    /// </summary>
    Channel = 2,
}
