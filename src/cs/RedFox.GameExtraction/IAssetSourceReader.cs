namespace RedFox.GameExtraction;

/// <summary>
/// Probes and opens a concrete asset source from an <see cref="AssetSourceRequest"/>.
/// </summary>
public interface IAssetSourceReader
{
    /// <summary>
    /// Determines whether the reader can open the supplied source request.
    /// </summary>
    /// <param name="request">The source request to inspect.</param>
    /// <returns><see langword="true"/> when the reader can open the request; otherwise, <see langword="false"/>.</returns>
    bool CanOpen(AssetSourceRequest request);

    /// <summary>
    /// Opens a source request and returns a populated asset source.
    /// </summary>
    /// <param name="request">The source request to open.</param>
    /// <param name="assetManager">The manager coordinating the mount, providing access to services and the shared virtual file system.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The mounted asset source.</returns>
    Task<IAssetSource> OpenAsync(
        AssetSourceRequest request,
        AssetManager assetManager,
        IProgress<string>? progress,
        CancellationToken cancellationToken);
}
