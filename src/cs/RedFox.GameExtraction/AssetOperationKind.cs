namespace RedFox.GameExtraction;

/// <summary>
/// Identifies the manager operation that failed.
/// </summary>
public enum AssetOperationKind
{
    /// <summary>
    /// A source mount operation failed.
    /// </summary>
    Mount,

    /// <summary>
    /// An asset read operation failed.
    /// </summary>
    Read,

    /// <summary>
    /// An asset export operation failed.
    /// </summary>
    Export,

    /// <summary>
    /// A source unload operation failed.
    /// </summary>
    Unload,
}
