namespace RedFox.GameExtraction;

/// <summary>
/// Handles reading and exporting a specific type of game asset.
/// Register implementations with <see cref="AssetManager.RegisterHandler"/>.
/// </summary>
public interface IAssetHandler
{
    /// <summary>
    /// Determines whether this handler is capable of processing the given entry.
    /// Implementations should inspect the file extension of <see cref="IAssetEntry.FullPath"/>
    /// or fields in <see cref="IAssetEntry.Metadata"/>.
    /// </summary>
    /// <param name="entry">The entry to evaluate.</param>
    /// <returns><see langword="true"/> if this handler can read and export the entry.</returns>
    bool CanHandle(IAssetEntry entry);

    /// <summary>
    /// Reads and decodes the asset data for the given entry.
    /// </summary>
    /// <param name="entry">The entry to load. Must satisfy <see cref="CanHandle"/>.</param>
    /// <param name="context">Operation context including optional export settings.</param>
    /// <param name="exportDirectory">The effective export directory for this read operation.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>An <see cref="AssetReadResult"/> containing the decoded asset data and any discovered reference entries.</returns>
    Task<AssetReadResult> ReadAsync(IAssetEntry entry, AssetOperationContext context, string? exportDirectory, CancellationToken token);

    /// <summary>
    /// Exports a previously decoded asset to disk.
    /// </summary>
    /// <param name="result">The result returned by <see cref="ReadAsync"/>.</param>
    /// <param name="context">Operation context including export configuration and flags.</param>
    /// <param name="exportDirectory">The effective export directory for this write operation.</param>
    /// <param name="token">Cancellation token.</param>
    Task WriteAsync(AssetReadResult result, AssetOperationContext context, string? exportDirectory, CancellationToken token);
}
