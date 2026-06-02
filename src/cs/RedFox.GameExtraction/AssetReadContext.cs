namespace RedFox.GameExtraction;

/// <summary>
/// Carries contextual information for asset read operations.
/// </summary>
public sealed class AssetReadContext
{
    private readonly List<AssetReadResult> _references = [];

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
    /// Gets the per-mount options associated with the owning source.
    /// </summary>
    public IReadOnlyDictionary<string, object?> SourceOptions => Request.Options;

    /// <summary>
    /// Gets optional user-defined data attached to the read operation.
    /// </summary>
    public object? UserData { get; }

    /// <summary>
    /// Gets the read results of any assets that were read as references through this context.
    /// </summary>
    /// <remarks>
    /// Populated as <see cref="ReadAsync(Asset, CancellationToken)"/> calls on this context resolve
    /// through the manager. The manager transfers the contents of this list into the
    /// <see cref="AssetReadResult.References"/> of the result produced by the active handler once
    /// the handler returns, so downstream consumers (e.g. <see cref="IAssetHandler.ExportAsync"/>) can
    /// inspect the references on the result itself. This property is exposed for handlers that want
    /// to observe references while the read is still in progress.
    /// </remarks>
    public IReadOnlyList<AssetReadResult> References => _references;

    internal AssetReadContext(
        AssetManager assetManager,
        IAssetSource source,
        AssetSourceRequest request)
        : this(assetManager, source, request, null)
    {
    }

    internal AssetReadContext(
        AssetManager assetManager,
        IAssetSource source,
        AssetSourceRequest request,
        object? userData)
    {
        AssetManager = assetManager;
        Source = source;
        Request = request;
        UserData = userData;
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

    /// <summary>
    /// Reads another asset through the owning manager and tracks the result as a reference on this context.
    /// </summary>
    /// <param name="asset">The asset to read.</param>
    /// <returns>The handler-produced read result.</returns>
    public Task<AssetReadResult> ReadAsync(Asset asset) =>
        AssetManager.ReadAsync(asset, this, CancellationToken.None);

    /// <summary>
    /// Reads another asset through the owning manager and tracks the result as a reference on this context.
    /// </summary>
    /// <param name="asset">The asset to read.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The handler-produced read result.</returns>
    public Task<AssetReadResult> ReadAsync(Asset asset, CancellationToken cancellationToken) =>
        AssetManager.ReadAsync(asset, this, cancellationToken);

    internal void AddReference(AssetReadResult result) => _references.Add(result);
}
