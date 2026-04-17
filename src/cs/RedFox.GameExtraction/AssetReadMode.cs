namespace RedFox.GameExtraction;

/// <summary>
/// Describes the intent of an asset read operation.
/// </summary>
public enum AssetReadMode
{
    /// <summary>
    /// Reads an asset for lightweight inspection or preview.
    /// </summary>
    Preview,

    /// <summary>
    /// Reads an asset for export.
    /// </summary>
    Export,
}
