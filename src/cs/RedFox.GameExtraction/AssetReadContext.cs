namespace RedFox.GameExtraction;

/// <summary>
/// Carries contextual information for asset read operations.
/// </summary>
public sealed class AssetReadContext
{
    /// <summary>
    /// Gets the manager coordinating the read.
    /// </summary>
    public AssetManager AssetManager { get; }

    /// <summary>
    /// Gets the source that owns the asset being read.
    /// </summary>
    public IAssetSource Source { get; }

    /// <summary>
    /// Gets the source request that produced the owning source.
    /// </summary>
    public AssetSourceRequest Request { get; }

    /// <summary>
    /// Gets the mode used for the read operation.
    /// </summary>
    public AssetReadMode Mode { get; }

    /// <summary>
    /// Gets the per-mount options associated with the owning source.
    /// </summary>
    public IReadOnlyDictionary<string, object?> SourceOptions => Request.Options;

    internal AssetReadContext(
        AssetManager assetManager,
        IAssetSource source,
        AssetSourceRequest request,
        AssetReadMode mode)
    {
        AssetManager = assetManager;
        Source = source;
        Request = request;
        Mode = mode;
    }

    /// <summary>
    /// Attempts to resolve a registered manager service.
    /// </summary>
    /// <typeparam name="T">The service type to resolve.</typeparam>
    /// <param name="service">The resolved service when one is registered.</param>
    /// <returns><see langword="true"/> when the service is available; otherwise, <see langword="false"/>.</returns>
    public bool TryGetService<T>(out T? service) where T : class => AssetManager.TryGetService(out service);

    /// <summary>
    /// Resolves a registered manager service or throws when one is not available.
    /// </summary>
    /// <typeparam name="T">The service type to resolve.</typeparam>
    /// <returns>The resolved service instance.</returns>
    public T GetRequiredService<T>() where T : class => AssetManager.GetRequiredService<T>();
}
