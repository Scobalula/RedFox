using System.Diagnostics.CodeAnalysis;

namespace RedFox.GameExtraction;

/// <summary>
/// Coordinates source mounting, asset reads, and asset export operations.
/// </summary>
public sealed class AssetManager
{
    private const int DefaultHeaderLength = 4096;

    private readonly List<IAssetSourceReader> _sourceReaders = [];
    private readonly List<IAssetHandler> _handlers = [];
    private readonly List<IAssetSource> _sources = [];
    private readonly Dictionary<IAssetSource, AssetSourceRequest> _sourceRequests = [];
    private readonly List<Asset> _assets = [];
    private readonly Dictionary<Type, ServiceRegistration> _services = [];

    /// <summary>
    /// Occurs after a source has been mounted successfully.
    /// </summary>
    public event EventHandler<SourceEventArgs>? SourceMounted;

    /// <summary>
    /// Occurs before a mounted source is unloaded.
    /// </summary>
    public event EventHandler<SourceEventArgs>? SourceUnloading;

    /// <summary>
    /// Occurs after a mounted source has been unloaded.
    /// </summary>
    public event EventHandler<SourceEventArgs>? SourceUnloaded;

    /// <summary>
    /// Occurs when an asset read is about to begin.
    /// </summary>
    public event EventHandler<AssetReadEventArgs>? AssetReadStarting;

    /// <summary>
    /// Occurs after an asset read completes successfully.
    /// </summary>
    public event EventHandler<AssetReadCompletedEventArgs>? AssetReadCompleted;

    /// <summary>
    /// Occurs when an asset export is about to begin.
    /// </summary>
    public event EventHandler<AssetExportEventArgs>? AssetExportStarting;

    /// <summary>
    /// Occurs after an asset export completes successfully or is skipped.
    /// </summary>
    public event EventHandler<AssetExportCompletedEventArgs>? AssetExportCompleted;

    /// <summary>
    /// Occurs when a manager operation fails.
    /// </summary>
    public event EventHandler<AssetOperationFailedEventArgs>? OperationFailed;

    /// <summary>
    /// Gets the registered source readers in evaluation order.
    /// </summary>
    public IReadOnlyList<IAssetSourceReader> SourceReaders => _sourceReaders;

    /// <summary>
    /// Gets the registered asset handlers in evaluation order.
    /// </summary>
    public IReadOnlyList<IAssetHandler> Handlers => _handlers;

    /// <summary>
    /// Gets the currently mounted sources.
    /// </summary>
    public IReadOnlyList<IAssetSource> Sources => _sources;

    /// <summary>
    /// Gets all assets from all mounted sources.
    /// </summary>
    public IReadOnlyList<Asset> Assets => _assets;

    /// <summary>
    /// Registers a source reader.
    /// </summary>
    /// <param name="reader">The source reader to register.</param>
    /// <remarks>Readers are evaluated in registration order, so more specific readers should be registered before generic fallbacks.</remarks>
    public void RegisterSourceReader(IAssetSourceReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _sourceReaders.Add(reader);
    }

    /// <summary>
    /// Registers an asset handler.
    /// </summary>
    /// <param name="handler">The asset handler to register.</param>
    public void RegisterHandler(IAssetHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handlers.Add(handler);

        foreach (Asset asset in _assets)
        {
            if (!asset.TryGetHandler(out _) && handler.CanHandle(asset))
            {
                asset.SetHandler(handler);
            }
        }
    }

    /// <summary>
    /// Registers a service instance for later retrieval.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    public void RegisterService<T>() where T : class, new() => RegisterService<T>(new T());

    /// <summary>
    /// Registers a service instance for later retrieval.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="service">The service instance to register.</param>
    public void RegisterService<T>(T service) where T : class
    {
        ArgumentNullException.ThrowIfNull(service);
        _services[typeof(T)] = ServiceRegistration.FromInstance(service);
    }

    /// <summary>
    /// Registers a lazy service factory.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="factory">The factory used to create the service instance.</param>
    public void RegisterServiceFactory<T>(Func<T> factory) where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        RegisterServiceFactory(_ => factory());
    }

    /// <summary>
    /// Registers a lazy service factory that can access the manager.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="factory">The factory used to create the service instance.</param>
    public void RegisterServiceFactory<T>(Func<AssetManager, T> factory) where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        _services[typeof(T)] = ServiceRegistration.FromFactory(manager => factory(manager));
    }

    /// <summary>
    /// Attempts to resolve a previously registered service.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <param name="service">The resolved service when one is available.</param>
    /// <returns><see langword="true"/> when the service is registered; otherwise, <see langword="false"/>.</returns>
    public bool TryGetService<T>([NotNullWhen(true)] out T? service) where T : class
    {
        service = null;
        
        if (_services.TryGetValue(typeof(T), out ServiceRegistration? registration) && registration.TryResolve(this, out object? resolved) && resolved is T typedService)
        {
            service = typedService;
            return true;
        }

        service = null;
        return false;
    }

    /// <summary>
    /// Resolves a previously registered service or throws when none is available.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>The resolved service instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the service has not been registered.</exception>
    public T GetRequiredService<T>() where T : class
    {
        if (TryGetService<T>(out T? service))
        {
            return service;
        }

        throw new InvalidOperationException($"No service of type '{typeof(T).FullName}' has been registered.");
    }

    /// <summary>
    /// Finds the first registered source reader that can open the supplied request.
    /// </summary>
    /// <param name="request">The source request to inspect.</param>
    /// <returns>The matching source reader, or <see langword="null"/> when none can open the request.</returns>
    public IAssetSourceReader? FindSourceReader(AssetSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Header = ReadHeader(request);

        foreach (IAssetSourceReader reader in _sourceReaders)
        {
            if (reader.CanOpen(request))
            {
                return reader;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the first registered handler that can process the supplied asset.
    /// </summary>
    /// <param name="asset">The asset to inspect.</param>
    /// <returns>The matching handler, or <see langword="null"/> when none can process the asset.</returns>
    public IAssetHandler? FindHandler(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        if (asset.TryGetHandler(out IAssetHandler? cachedHandler))
        {
            return cachedHandler;
        }

        foreach (IAssetHandler handler in _handlers)
        {
            if (handler.CanHandle(asset))
            {
                asset.SetHandler(handler);
                return handler;
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to retrieve the request that was used to mount a source.
    /// </summary>
    /// <param name="source">The mounted source.</param>
    /// <param name="request">The source request when one is found.</param>
    /// <returns><see langword="true"/> when the request is available; otherwise, <see langword="false"/>.</returns>
    public bool TryGetSourceRequest(IAssetSource source, [NotNullWhen(true)] out AssetSourceRequest? request)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _sourceRequests.TryGetValue(source, out request);
    }

    /// <summary>
    /// Mounts a file-backed source request.
    /// </summary>
    /// <param name="path">The file to mount.</param>
    /// <returns>The mounted source.</returns>
    public Task<IAssetSource> MountFileAsync(string path) =>
        MountFileAsync(path, null, null, CancellationToken.None);

    /// <summary>
    /// Mounts a file-backed source request.
    /// </summary>
    /// <param name="path">The file to mount.</param>
    /// <param name="options">Optional per-mount options.</param>
    /// <returns>The mounted source.</returns>
    public Task<IAssetSource> MountFileAsync(string path, IReadOnlyDictionary<string, object?> options) =>
        MountFileAsync(path, options, null, CancellationToken.None);

    /// <summary>
    /// Mounts a file-backed source request.
    /// </summary>
    /// <param name="path">The file to mount.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <returns>The mounted source.</returns>
    public Task<IAssetSource> MountFileAsync(string path, IProgress<string> progress) =>
        MountFileAsync(path, null, progress, CancellationToken.None);

    /// <summary>
    /// Mounts a file-backed source request.
    /// </summary>
    /// <param name="path">The file to mount.</param>
    /// <param name="options">Optional per-mount options.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The mounted source.</returns>
    public Task<IAssetSource> MountFileAsync(string path, IReadOnlyDictionary<string, object?>? options, IProgress<string>? progress, CancellationToken cancellationToken) =>
        MountAsync(AssetSourceRequest.ForFile(path, options), progress, cancellationToken);

    /// <summary>
    /// Mounts a directory-backed source request.
    /// </summary>
    /// <param name="path">The directory to mount.</param>
    /// <returns>The mounted source.</returns>
    public Task<IAssetSource> MountDirectoryAsync(string path) =>
        MountDirectoryAsync(path, null, null, CancellationToken.None);

    /// <summary>
    /// Mounts a directory-backed source request.
    /// </summary>
    /// <param name="path">The directory to mount.</param>
    /// <param name="options">Optional per-mount options.</param>
    /// <returns>The mounted source.</returns>
    public Task<IAssetSource> MountDirectoryAsync(string path, IReadOnlyDictionary<string, object?> options) =>
        MountDirectoryAsync(path, options, null, CancellationToken.None);

    /// <summary>
    /// Mounts a directory-backed source request.
    /// </summary>
    /// <param name="path">The directory to mount.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <returns>The mounted source.</returns>
    public Task<IAssetSource> MountDirectoryAsync(string path, IProgress<string> progress) =>
        MountDirectoryAsync(path, null, progress, CancellationToken.None);

    /// <summary>
    /// Mounts a directory-backed source request.
    /// </summary>
    /// <param name="path">The directory to mount.</param>
    /// <param name="options">Optional per-mount options.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The mounted source.</returns>
    public Task<IAssetSource> MountDirectoryAsync(string path, IReadOnlyDictionary<string, object?>? options, IProgress<string>? progress, CancellationToken cancellationToken) =>
        MountAsync(AssetSourceRequest.ForDirectory(path, options), progress, cancellationToken);

    /// <summary>
    /// Mounts a process-backed source request using a process identifier.
    /// </summary>
    /// <param name="processId">The process identifier to mount.</param>
    /// <returns>The mounted source.</returns>
    public Task<IAssetSource> MountProcessAsync(int processId) =>
        MountProcessAsync(processId, null, null, CancellationToken.None);

    /// <summary>
    /// Mounts a process-backed source request using a process identifier.
    /// </summary>
    /// <param name="processId">The process identifier to mount.</param>
    /// <param name="options">Optional per-mount options.</param>
    /// <returns>The mounted source.</returns>
    public Task<IAssetSource> MountProcessAsync(int processId, IReadOnlyDictionary<string, object?> options) =>
        MountProcessAsync(processId, options, null, CancellationToken.None);

    /// <summary>
    /// Mounts a process-backed source request using a process identifier.
    /// </summary>
    /// <param name="processId">The process identifier to mount.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <returns>The mounted source.</returns>
    public Task<IAssetSource> MountProcessAsync(int processId, IProgress<string> progress) =>
        MountProcessAsync(processId, null, progress, CancellationToken.None);

    /// <summary>
    /// Mounts a process-backed source request using a process identifier.
    /// </summary>
    /// <param name="processId">The process identifier to mount.</param>
    /// <param name="options">Optional per-mount options.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The mounted source.</returns>
    public Task<IAssetSource> MountProcessAsync(int processId, IReadOnlyDictionary<string, object?>? options, IProgress<string>? progress, CancellationToken cancellationToken) =>
        MountAsync(AssetSourceRequest.ForProcess(processId, options), progress, cancellationToken);

    /// <summary>
    /// Mounts a process-backed source request using a process name.
    /// </summary>
    /// <param name="processName">The process name to mount.</param>
    /// <returns>The mounted source.</returns>
    public Task<IAssetSource> MountProcessAsync(string processName) =>
        MountProcessAsync(processName, null, null, CancellationToken.None);

    /// <summary>
    /// Mounts a process-backed source request using a process name.
    /// </summary>
    /// <param name="processName">The process name to mount.</param>
    /// <param name="options">Optional per-mount options.</param>
    /// <returns>The mounted source.</returns>
    public Task<IAssetSource> MountProcessAsync(string processName, IReadOnlyDictionary<string, object?> options) =>
        MountProcessAsync(processName, options, null, CancellationToken.None);

    /// <summary>
    /// Mounts a process-backed source request using a process name.
    /// </summary>
    /// <param name="processName">The process name to mount.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <returns>The mounted source.</returns>
    public Task<IAssetSource> MountProcessAsync(string processName, IProgress<string> progress) =>
        MountProcessAsync(processName, null, progress, CancellationToken.None);

    /// <summary>
    /// Mounts a process-backed source request using a process name.
    /// </summary>
    /// <param name="processName">The process name to mount.</param>
    /// <param name="options">Optional per-mount options.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The mounted source.</returns>
    public Task<IAssetSource> MountProcessAsync(string processName, IReadOnlyDictionary<string, object?>? options, IProgress<string>? progress, CancellationToken cancellationToken) =>
        MountAsync(AssetSourceRequest.ForProcess(processName, options), progress, cancellationToken);

    /// <summary>
    /// Mounts a source request using the first compatible registered reader.
    /// </summary>
    /// <param name="request">The source request to mount.</param>
    /// <returns>The mounted source.</returns>
    public Task<IAssetSource> MountAsync(AssetSourceRequest request) =>
        MountAsync(request, null, CancellationToken.None);

    /// <summary>
    /// Mounts a source request using the first compatible registered reader.
    /// </summary>
    /// <param name="request">The source request to mount.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The mounted source.</returns>
    public async Task<IAssetSource> MountAsync(AssetSourceRequest request, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        IAssetSourceReader reader = FindSourceReader(request) ?? throw new NotSupportedException($"No registered source reader can open {request.Description}.");

        IAssetSource source;
        try
        {
            source = await reader.OpenAsync(request, this, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            OperationFailed?.Invoke(this, new AssetOperationFailedEventArgs(AssetOperationKind.Mount, ex));
            throw;
        }

        try
        {
            ValidateAssets(source.Assets);
            RegisterSource(source, request);
            SourceMounted?.Invoke(this, new SourceEventArgs(source));
            return source;
        }
        catch
        {
            await source.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Unloads a mounted source and removes its assets.
    /// </summary>
    /// <param name="source">The source to unload.</param>
    /// <returns><see langword="true"/> when the source was unloaded; otherwise, <see langword="false"/>.</returns>
    public Task<bool> UnloadAsync(IAssetSource source) => UnloadAsync(source, CancellationToken.None);

    /// <summary>
    /// Unloads a mounted source and removes its assets.
    /// </summary>
    /// <param name="source">The source to unload.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns><see langword="true"/> when the source was unloaded; otherwise, <see langword="false"/>.</returns>
    public async Task<bool> UnloadAsync(IAssetSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_sources.Contains(source))
        {
            return false;
        }

        SourceUnloading?.Invoke(this, new SourceEventArgs(source));

        try
        {
            await source.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            OperationFailed?.Invoke(this, new AssetOperationFailedEventArgs(AssetOperationKind.Unload, ex, source, null, null));
            throw;
        }

        UnregisterSource(source);
        SourceUnloaded?.Invoke(this, new SourceEventArgs(source));
        return true;
    }

    /// <summary>
    /// Reads an asset using the first compatible registered handler.
    /// </summary>
    /// <param name="asset">The asset to read.</param>
    /// <returns>The handler-produced read result.</returns>
    public Task<AssetReadResult> ReadAsync(Asset asset) =>
        ReadAsync(asset, parentContext: null, userData: null, CancellationToken.None);

    /// <summary>
    /// Reads an asset using the first compatible registered handler.
    /// </summary>
    /// <param name="asset">The asset to read.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The handler-produced read result.</returns>
    public Task<AssetReadResult> ReadAsync(Asset asset, CancellationToken cancellationToken) =>
        ReadAsync(asset, parentContext: null, userData: null, cancellationToken);

    /// <summary>
    /// Reads an asset using the first compatible registered handler, carrying optional user-defined data through the read context.
    /// </summary>
    /// <param name="asset">The asset to read.</param>
    /// <param name="userData">Optional user-defined data attached to the read operation.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The handler-produced read result.</returns>
    public Task<AssetReadResult> ReadAsync(Asset asset, object? userData, CancellationToken cancellationToken) =>
        ReadAsync(asset, parentContext: null, userData, cancellationToken);

    /// <summary>
    /// Reads an asset using the first compatible registered handler, recording the result as a reference on the supplied parent context.
    /// </summary>
    /// <param name="asset">The asset to read.</param>
    /// <param name="parentContext">
    /// The parent read context to which the result should be appended as a reference, or <see langword="null"/>
    /// when no tracking is required. When a handler calls <see cref="AssetReadContext.ReadAsync(Asset, CancellationToken)"/>
    /// on its own context, the manager uses that context as the parent and the result lands in
    /// <see cref="AssetReadContext.References"/>.
    /// </param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The handler-produced read result.</returns>
    public Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext? parentContext, CancellationToken cancellationToken) =>
        ReadAsync(asset, parentContext, userData: null, cancellationToken);

    /// <summary>
    /// Reads an asset using the first compatible registered handler, recording the result as a reference on the supplied parent context.
    /// </summary>
    /// <param name="asset">The asset to read.</param>
    /// <param name="parentContext">
    /// The parent read context to which the result should be appended as a reference, or <see langword="null"/>
    /// when no tracking is required. When a handler calls <see cref="AssetReadContext.ReadAsync(Asset, CancellationToken)"/>
    /// on its own context, the manager uses that context as the parent and the result lands in
    /// <see cref="AssetReadContext.References"/> and, after the handler returns, in
    /// <see cref="AssetReadResult.References"/>.
    /// </param>
    /// <param name="userData">Optional user-defined data attached to the read operation.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The handler-produced read result.</returns>
    public async Task<AssetReadResult> ReadAsync(Asset asset, AssetReadContext? parentContext, object? userData, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        cancellationToken.ThrowIfCancellationRequested();

        IAssetSource source = GetRequiredSource(asset);
        IAssetHandler handler = GetRequiredHandler(asset);
        AssetSourceRequest sourceRequest = GetRequiredSourceRequest(source);
        AssetReadStarting?.Invoke(this, new AssetReadEventArgs(asset, source));

        try
        {
            AssetReadContext context = new(this, source, sourceRequest, userData);
            AssetReadResult result = await handler.ReadAsync(asset, context, cancellationToken).ConfigureAwait(false);

            foreach (AssetReadResult reference in context.References)
            {
                result.AddReference(reference);
            }

            parentContext?.AddReference(result);

            AssetReadCompleted?.Invoke(this, new AssetReadCompletedEventArgs(asset, source, result));
            return result;
        }
        catch (Exception ex)
        {
            OperationFailed?.Invoke(this, new AssetOperationFailedEventArgs(AssetOperationKind.Read, ex, source, asset, null));
            throw;
        }
    }

    /// <summary>
    /// Exports a single asset.
    /// </summary>
    /// <param name="asset">The asset to export.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    /// <returns>A task that completes when the export finishes.</returns>
    public Task ExportAsync(Asset asset, ExportConfiguration configuration) =>
        ExportAsync(asset, configuration, null, CancellationToken.None);

    /// <summary>
    /// Exports a single asset.
    /// </summary>
    /// <param name="asset">The asset to export.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    /// <param name="progress">An optional progress sink.</param>
    public Task ExportAsync(Asset asset, ExportConfiguration configuration, IProgress<string> progress) =>
        ExportAsync(asset, configuration, progress, CancellationToken.None);

    /// <summary>
    /// Exports a single asset.
    /// </summary>
    /// <param name="asset">The asset to export.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    public Task ExportAsync(Asset asset, ExportConfiguration configuration, CancellationToken cancellationToken) =>
        ExportAsync(asset, configuration, null, cancellationToken);

    /// <summary>
    /// Exports a single asset.
    /// </summary>
    /// <param name="asset">The asset to export.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    public Task ExportAsync(Asset asset, ExportConfiguration configuration, IProgress<string>? progress, CancellationToken cancellationToken) =>
        ExportAsync(asset, string.Empty, configuration, progress, cancellationToken);

    /// <summary>
    /// Exports a single asset using a pre-computed read result, bypassing the manager's read step.
    /// </summary>
    /// <param name="asset">The asset to export.</param>
    /// <param name="result">A read result whose <see cref="AssetReadResult.Asset"/> matches <paramref name="asset"/>.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    public Task ExportAsync(Asset asset, AssetReadResult result, ExportConfiguration configuration) =>
        ExportAsync(asset, result, configuration, null, CancellationToken.None);

    /// <summary>
    /// Exports a single asset using a pre-computed read result, bypassing the manager's read step.
    /// </summary>
    /// <param name="asset">The asset to export.</param>
    /// <param name="result">A read result whose <see cref="AssetReadResult.Asset"/> matches <paramref name="asset"/>.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    /// <param name="progress">An optional progress sink.</param>
    public Task ExportAsync(Asset asset, AssetReadResult result, ExportConfiguration configuration, IProgress<string> progress) =>
        ExportAsync(asset, result, configuration, progress, CancellationToken.None);

    /// <summary>
    /// Exports a single asset using a pre-computed read result, bypassing the manager's read step.
    /// </summary>
    /// <param name="asset">The asset to export.</param>
    /// <param name="result">A read result whose <see cref="AssetReadResult.Asset"/> matches <paramref name="asset"/>.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    public Task ExportAsync(Asset asset, AssetReadResult result, ExportConfiguration configuration, CancellationToken cancellationToken) =>
        ExportAsync(asset, result, configuration, null, cancellationToken);

    /// <summary>
    /// Exports a single asset using a pre-computed read result, bypassing the manager's read step.
    /// </summary>
    /// <param name="asset">The asset to export.</param>
    /// <param name="result">A read result whose <see cref="AssetReadResult.Asset"/> matches <paramref name="asset"/>.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    public async Task ExportAsync(Asset asset, AssetReadResult result, ExportConfiguration configuration, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!ReferenceEquals(result.Asset, asset))
        {
            throw new ArgumentException($"The supplied result belongs to '{result.Asset.Name}' but the asset being exported is '{asset.Name}'.", nameof(result));
        }

        await ExportAsync(asset, result, string.Empty, configuration, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Exports an asset identified by its read result, bypassing the manager's read step.
    /// </summary>
    /// <param name="result">The read result to export. The asset is taken from <see cref="AssetReadResult.Asset"/>.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    public Task ExportAsync(AssetReadResult result, ExportConfiguration configuration) =>
        ExportAsync(result, string.Empty, configuration, null, CancellationToken.None);

    /// <summary>
    /// Exports an asset identified by its read result, bypassing the manager's read step.
    /// </summary>
    /// <param name="result">The read result to export. The asset is taken from <see cref="AssetReadResult.Asset"/>.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    /// <param name="progress">An optional progress sink.</param>
    public Task ExportAsync(AssetReadResult result, ExportConfiguration configuration, IProgress<string> progress) =>
        ExportAsync(result, string.Empty, configuration, progress, CancellationToken.None);

    /// <summary>
    /// Exports an asset identified by its read result, bypassing the manager's read step.
    /// </summary>
    /// <param name="result">The read result to export. The asset is taken from <see cref="AssetReadResult.Asset"/>.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    public Task ExportAsync(AssetReadResult result, ExportConfiguration configuration, CancellationToken cancellationToken) =>
        ExportAsync(result, string.Empty, configuration, null, cancellationToken);

    /// <summary>
    /// Exports an asset identified by its read result, bypassing the manager's read step.
    /// </summary>
    /// <param name="result">The read result to export. The asset is taken from <see cref="AssetReadResult.Asset"/>.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    public Task ExportAsync(AssetReadResult result, ExportConfiguration configuration, IProgress<string>? progress, CancellationToken cancellationToken) =>
        ExportAsync(result, string.Empty, configuration, progress, cancellationToken);

    /// <summary>
    /// Exports an asset identified by its read result into a relative output directory, bypassing the manager's read step.
    /// </summary>
    /// <param name="result">The read result to export. The asset is taken from <see cref="AssetReadResult.Asset"/>.</param>
    /// <param name="relativeOutputDirectory">The relative output directory rooted at the configuration's output directory.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    public Task ExportAsync(AssetReadResult result, string relativeOutputDirectory, ExportConfiguration configuration, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        return ExportAsync(result.Asset, result, relativeOutputDirectory, configuration, progress, cancellationToken);
    }

    /// <summary>
    /// Exports a collection of assets.
    /// </summary>
    /// <param name="assets">The assets to export.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    public Task ExportAsync(IEnumerable<Asset> assets, ExportConfiguration configuration) =>
        ExportAsync(assets, configuration, null, CancellationToken.None);

    /// <summary>
    /// Exports a collection of assets.
    /// </summary>
    /// <param name="assets">The assets to export.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    /// <param name="progress">An optional progress sink.</param>
    public Task ExportAsync(IEnumerable<Asset> assets, ExportConfiguration configuration, IProgress<string> progress) =>
        ExportAsync(assets, configuration, progress, CancellationToken.None);

    /// <summary>
    /// Exports a collection of assets.
    /// </summary>
    /// <param name="assets">The assets to export.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    public Task ExportAsync(IEnumerable<Asset> assets, ExportConfiguration configuration, CancellationToken cancellationToken) =>
        ExportAsync(assets, configuration, null, cancellationToken);

    /// <summary>
    /// Exports a collection of assets.
    /// </summary>
    /// <param name="assets">The assets to export.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    public async Task ExportAsync(IEnumerable<Asset> assets, ExportConfiguration configuration, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentNullException.ThrowIfNull(configuration);

        foreach (Asset asset in assets)
        {
            await ExportAsync(asset, string.Empty, configuration, progress, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Per-asset export pipeline called by every public <see cref="ExportAsync(Asset, ExportConfiguration)"/>
    /// overload and by <see cref="AssetExportContext"/> for recursive exports. Reads the asset via the handler.
    /// </summary>
    /// <param name="asset">The asset to export.</param>
    /// <param name="relativeOutputDirectory">The relative output directory rooted at the configuration's output directory.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    public Task ExportAsync(Asset asset, string relativeOutputDirectory, ExportConfiguration configuration, IProgress<string>? progress, CancellationToken cancellationToken) =>
        ExportAsync(asset, relativeOutputDirectory, configuration, progress, cancellationToken, null);

    /// <summary>
    /// Per-asset export pipeline called by every public <see cref="ExportAsync(Asset, ExportConfiguration)"/>
    /// overload and by <see cref="AssetExportContext"/> for recursive exports. Reads the asset via the handler.
    /// </summary>
    /// <param name="asset">The asset to export.</param>
    /// <param name="relativeOutputDirectory">The relative output directory rooted at the configuration's output directory.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <param name="userData">Optional user-defined data attached to the export operation.</param>
    public async Task ExportAsync(Asset asset, string relativeOutputDirectory, ExportConfiguration configuration, IProgress<string>? progress, CancellationToken cancellationToken, object? userData)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(configuration);

        cancellationToken.ThrowIfCancellationRequested();

        IAssetSource source = GetRequiredSource(asset);
        string normalizedDir = NormalizeRelativeOutputDirectory(relativeOutputDirectory);
        IAssetHandler handler = GetRequiredHandler(asset);
        AssetSourceRequest sourceRequest = GetRequiredSourceRequest(source);
        AssetExportContext exportContext = new(this, source, sourceRequest, configuration, normalizedDir, progress, cancellationToken, userData);

        AssetExportStarting?.Invoke(this, new AssetExportEventArgs(asset, source, configuration, normalizedDir));

        bool shouldExport = await handler.ShouldExportAsync(asset, exportContext, cancellationToken).ConfigureAwait(false);

        if (!shouldExport)
        {
            progress?.Report(string.IsNullOrWhiteSpace(normalizedDir) ? $"Skipped {asset.Name}" : $"Skipped {asset.Name} -> {normalizedDir}");
            AssetExportCompleted?.Invoke(this, new AssetExportCompletedEventArgs(asset, source, configuration, normalizedDir, skipped: true));
            return;
        }

        AssetReadResult result = await ReadAsync(asset, userData, cancellationToken).ConfigureAwait(false);

        progress?.Report(string.IsNullOrWhiteSpace(normalizedDir) ? $"Exporting {asset.Name}" : $"Exporting {asset.Name} -> {normalizedDir}");

        try
        {
            await handler.ExportAsync(result, exportContext, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            OperationFailed?.Invoke(this, new AssetOperationFailedEventArgs(AssetOperationKind.Export, ex, source, asset, normalizedDir));
            throw;
        }

        AssetExportCompleted?.Invoke(this, new AssetExportCompletedEventArgs(asset, source, configuration, normalizedDir, skipped: false));
    }

    /// <summary>
    /// Exports a single asset using a pre-read result, skipping the read step.
    /// </summary>
    /// <param name="asset">The asset to export.</param>
    /// <param name="result">The already-read result to export.</param>
    /// <param name="relativeOutputDirectory">The relative output directory rooted at the configuration's output directory.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    public Task ExportAsync(Asset asset, AssetReadResult result, string relativeOutputDirectory, ExportConfiguration configuration, IProgress<string>? progress, CancellationToken cancellationToken) =>
        ExportAsync(asset, result, relativeOutputDirectory, configuration, progress, cancellationToken, null);

    /// <summary>
    /// Exports a single asset using a pre-read result, skipping the read step.
    /// </summary>
    /// <param name="asset">The asset to export.</param>
    /// <param name="result">The already-read result to export.</param>
    /// <param name="relativeOutputDirectory">The relative output directory rooted at the configuration's output directory.</param>
    /// <param name="configuration">The export configuration to apply.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <param name="userData">Optional user-defined data attached to the export operation.</param>
    public async Task ExportAsync(Asset asset, AssetReadResult result, string relativeOutputDirectory, ExportConfiguration configuration, IProgress<string>? progress, CancellationToken cancellationToken, object? userData)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(configuration);

        cancellationToken.ThrowIfCancellationRequested();

        IAssetSource source = GetRequiredSource(asset);
        string normalizedDir = NormalizeRelativeOutputDirectory(relativeOutputDirectory);
        IAssetHandler handler = GetRequiredHandler(asset);
        AssetSourceRequest sourceRequest = GetRequiredSourceRequest(source);
        AssetExportContext exportContext = new(this, source, sourceRequest, configuration, normalizedDir, progress, cancellationToken, userData);

        AssetExportStarting?.Invoke(this, new AssetExportEventArgs(asset, source, configuration, normalizedDir));

        bool shouldExport = await handler.ShouldExportAsync(asset, exportContext, cancellationToken).ConfigureAwait(false);

        if (!shouldExport)
        {
            progress?.Report(string.IsNullOrWhiteSpace(normalizedDir) ? $"Skipped {asset.Name}" : $"Skipped {asset.Name} -> {normalizedDir}");
            AssetExportCompleted?.Invoke(this, new AssetExportCompletedEventArgs(asset, source, configuration, normalizedDir, skipped: true));
            return;
        }

        progress?.Report(string.IsNullOrWhiteSpace(normalizedDir) ? $"Exporting {asset.Name}" : $"Exporting {asset.Name} -> {normalizedDir}");

        try
        {
            await handler.ExportAsync(result, exportContext, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            OperationFailed?.Invoke(this, new AssetOperationFailedEventArgs(AssetOperationKind.Export, ex, source, asset, normalizedDir));
            throw;
        }

        AssetExportCompleted?.Invoke(this, new AssetExportCompletedEventArgs(asset, source, configuration, normalizedDir, skipped: false));
    }

    internal static string NormalizeVirtualPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string[] parts = path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            throw new ArgumentException("The supplied path does not contain any valid segments.", nameof(path));
        }

        return string.Join(Path.DirectorySeparatorChar, parts);
    }

    internal static string NormalizeRelativeOutputDirectory(string? relativeOutputDirectory)
    {
        if (string.IsNullOrWhiteSpace(relativeOutputDirectory))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(relativeOutputDirectory))
        {
            throw new ArgumentException("Output directories must be relative.", nameof(relativeOutputDirectory));
        }

        string[] parts = relativeOutputDirectory.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length == 0 ? string.Empty : Path.Combine(parts);
    }

    internal static string CombineRelativePaths(string first, string second)
    {
        string normalizedFirst = NormalizeRelativeOutputDirectory(first);
        string normalizedSecond = NormalizeRelativeOutputDirectory(second);

        if (string.IsNullOrWhiteSpace(normalizedFirst))
        {
            return normalizedSecond;
        }

        if (string.IsNullOrWhiteSpace(normalizedSecond))
        {
            return normalizedFirst;
        }

        return Path.Combine(normalizedFirst, normalizedSecond);
    }

    /// <summary>
    /// Returns the mounted source that owns the supplied asset, or throws when it is not available.
    /// </summary>
    /// <param name="asset">The asset whose source to retrieve.</param>
    /// <returns>The mounted source that owns <paramref name="asset"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the asset has no attached source or the source is not mounted.</exception>
    public IAssetSource GetRequiredSource(Asset asset)
    {
        IAssetSource source = asset.Source ?? throw new InvalidOperationException($"The asset '{asset.Name}' is not attached to a mounted source.");

        if (_sources.Contains(source))
        {
            return source;
        }

        throw new InvalidOperationException($"The source for asset '{asset.Name}' is not mounted.");
    }

    /// <summary>
    /// Returns the first registered handler that can process the supplied asset, or throws when none is available.
    /// </summary>
    /// <param name="asset">The asset to process.</param>
    /// <returns>The matching handler.</returns>
    /// <exception cref="NotSupportedException">Thrown when no registered handler can process the asset.</exception>
    public IAssetHandler GetRequiredHandler(Asset asset) =>
        FindHandler(asset) ?? throw new NotSupportedException($"No registered asset handler can process '{asset.Name}'.");

    /// <summary>
    /// Returns the request that was used to mount the supplied source, or throws when none is associated.
    /// </summary>
    /// <param name="source">The mounted source.</param>
    /// <returns>The source request associated with <paramref name="source"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the source has no associated request.</exception>
    public AssetSourceRequest GetRequiredSourceRequest(IAssetSource source) =>
        _sourceRequests.TryGetValue(source, out AssetSourceRequest? request) ? request : throw new InvalidOperationException($"No request is associated with the source '{source.Name}'.");

    private static void ValidateAssets(IReadOnlyList<Asset> assets)
    {
        HashSet<string> seenPaths = new(StringComparer.OrdinalIgnoreCase);

        foreach (Asset asset in assets)
        {
            string normalizedPath = NormalizeVirtualPath(asset.Name);

            if (!seenPaths.Add(normalizedPath))
            {
                throw new InvalidOperationException($"An asset with the path '{asset.Name}' has already been registered for this source.");
            }
        }
    }

    private void RegisterSource(IAssetSource source, AssetSourceRequest request)
    {
        _sources.Add(source);
        _sourceRequests[source] = request;

        foreach (Asset asset in source.Assets)
        {
            asset.AttachSource(source);
            _assets.Add(asset);

            if (!asset.TryGetHandler(out _))
            {
                FindHandler(asset);
            }
        }
    }

    private void UnregisterSource(IAssetSource source)
    {
        _sourceRequests.Remove(source);
        _sources.Remove(source);

        for (int index = _assets.Count - 1; index >= 0; index--)
        {
            Asset asset = _assets[index];
            if (!ReferenceEquals(asset.Source, source))
            {
                continue;
            }

            asset.DetachSource();
            _assets.RemoveAt(index);
        }
    }

    private static byte[] ReadHeader(AssetSourceRequest request)
    {
        if (request.Kind != AssetSourceKind.File || string.IsNullOrWhiteSpace(request.Location))
        {
            return [];
        }

        try
        {
            using FileStream stream = File.OpenRead(request.Location);
            int length = (int)Math.Min(stream.Length, DefaultHeaderLength);
            if (length <= 0)
            {
                return [];
            }

            byte[] buffer = GC.AllocateUninitializedArray<byte>(length);
            int read = stream.Read(buffer, 0, length);
            return read <= 0 ? [] : read == length ? buffer : buffer[..read];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
        catch (NotSupportedException)
        {
            return [];
        }
    }
}
