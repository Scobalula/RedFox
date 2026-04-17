namespace RedFox.GameExtraction;

/// <summary>
/// Identifies the kind of source represented by an <see cref="AssetSourceRequest"/>.
/// </summary>
public enum AssetSourceKind
{
    /// <summary>
    /// A file-backed source such as an archive or package.
    /// </summary>
    File,

    /// <summary>
    /// A directory-backed source on disk.
    /// </summary>
    Directory,

    /// <summary>
    /// A process-backed source such as a running game.
    /// </summary>
    Process,
}
