namespace RedFox.GameExtraction;

/// <summary>
/// Represents an opened game asset source: a file archive, VFS bundle, or memory-mapped region.
/// Provides a flat enumeration of all contained <see cref="IAssetEntry"/> items.
/// </summary>
public interface IAssetSource : IDisposable
{
    /// <summary>Display name for this source (e.g., archive file name or process name).</summary>
    string Name { get; }

    /// <summary>All asset entries contained in this source.</summary>
    IReadOnlyList<IAssetEntry> Entries { get; }

    /// <summary>
    /// Attempts to locate an entry by its full virtual path.
    /// </summary>
    /// <param name="path">The full virtual path to look up.</param>
    /// <param name="entry">The matching entry, or <see langword="null"/> if not found.</param>
    /// <returns><see langword="true"/> if a matching entry was found; otherwise <see langword="false"/>.</returns>
    bool TryGetEntry(string path, out IAssetEntry? entry);
}
