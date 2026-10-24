namespace RedFox.GameExtraction;

/// <summary>
/// Represents a mounted source whose assets include sources of their own, such as the archives inside a game's storage, which can be mounted without exporting them first.
/// </summary>
public interface IAssetSourceContainer
{
    /// <summary>
    /// Gets the assets that can be mounted as sources.
    /// </summary>
    IReadOnlyList<Asset> MountableAssets { get; }

    /// <summary>
    /// Prepares an asset to be mounted, such as by extracting it, and returns the request that mounts it.
    /// </summary>
    /// <param name="asset">One of <see cref="MountableAssets"/>.</param>
    /// <param name="options">The source options to give the request.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The request that mounts the asset.</returns>
    Task<AssetSourceRequest> GetSourceRequestAsync(Asset asset, IReadOnlyDictionary<string, object?>? options, CancellationToken cancellationToken);
}
