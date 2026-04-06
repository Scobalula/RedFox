using RedFox.IO.FileSystem;
using System.Diagnostics.CodeAnalysis;

namespace RedFox.GameExtraction;

/// <summary>
/// Central coordinator for game asset operations.
/// Maintains a shared <see cref="VirtualFileSystem"/>, registries of
/// <see cref="IAssetSourceReader"/> and <see cref="IAssetHandler"/> implementations,
/// and orchestrates mounting, reading, and exporting game assets.
/// </summary>
public sealed class AssetManager
{
    private readonly List<IAssetSourceReader> _sourceReaders = [];
    private readonly List<IAssetHandler> _handlers = [];
    private readonly Dictionary<Type, object> _services = [];

    /// <summary>
    /// The shared virtual file system. <see langword="null"/> until a VFS-aware reader
    /// calls <see cref="EnsureFileSystem"/> during mounting.
    /// </summary>
    public VirtualFileSystem? FileSystem { get; private set; }

    /// <summary>
    /// Returns <see cref="FileSystem"/>, creating it on first call.
    /// VFS-aware <see cref="IAssetSourceReader"/> implementations should use this
    /// rather than accessing <see cref="FileSystem"/> directly.
    /// </summary>
    public VirtualFileSystem EnsureFileSystem() => FileSystem ??= new VirtualFileSystem();

    // -------------------------------------------------------------------------
    // Registration
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers an <see cref="IAssetSourceReader"/> for opening archive sources.
    /// Readers are tried in registration order; the first to report <see cref="IAssetSourceReader.CanRead"/>
    /// wins.
    /// </summary>
    /// <param name="reader">The reader to register.</param>
    public void RegisterSourceReader(IAssetSourceReader reader) => _sourceReaders.Add(reader);

    /// <summary>
    /// Registers an <see cref="IAssetHandler"/> for reading and exporting a specific asset type.
    /// Handlers are tried in registration order; the first to report <see cref="IAssetHandler.CanHandle"/>
    /// wins.
    /// </summary>
    /// <param name="handler">The handler to register.</param>
    public void RegisterHandler(IAssetHandler handler) => _handlers.Add(handler);

    /// <summary>
    /// Registers a singleton service instance for handlers to consume through operation context.
    /// </summary>
    public void RegisterService<T>(T service) where T : class
    {
        ArgumentNullException.ThrowIfNull(service);
        _services[typeof(T)] = service;
    }

    /// <summary>
    /// Attempts to resolve a previously registered service.
    /// </summary>
    public bool TryGetService<T>([NotNullWhen(true)] out T? service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var obj) && obj is T typed)
        {
            service = typed;
            return true;
        }

        service = null;
        return false;
    }

    // -------------------------------------------------------------------------
    // Mounting
    // -------------------------------------------------------------------------

    /// <summary>
    /// Mounts a file-based asset source into the manager.
    /// The first registered <see cref="IAssetSourceReader"/> that accepts the path is used.
    /// VFS-aware readers will also populate <see cref="FileSystem"/> during this call.
    /// </summary>
    /// <remarks>
    /// The file stream's ownership transfers to the returned <see cref="IAssetSource"/> on
    /// success and is disposed when the source is disposed. If mounting fails the stream
    /// is disposed automatically.
    /// </remarks>
    /// <param name="path">Absolute path to the archive file.</param>
    /// <param name="progress">Optional progress reporter for the enumeration phase.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="LoadedSource"/> containing the mounted source and its entries.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when no registered reader can handle the given file.
    /// </exception>
    public async Task<LoadedSource> MountArchiveAsync(string path, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var reader = FindSourceReader(path) ?? throw new NotSupportedException($"No registered source reader supports '{Path.GetFileName(path)}'.");

        using var stream = File.OpenRead(path);
        var source = await reader.ReadAsync(stream, path, this, progress, cancellationToken).ConfigureAwait(false);
        return new LoadedSource { Source = source, Location = path };
    }

    /// <summary>
    /// Mounts a memory (process) asset source into the manager.
    /// </summary>
    /// <param name="source">The already-constructed process source to mount.</param>
    /// <returns>A <see cref="LoadedSource"/> containing the mounted source and its entries.</returns>
    public LoadedSource MountProcess(IAssetSource source) =>
        new() { Source = source, Location = string.Empty };

    // -------------------------------------------------------------------------
    // Discovery
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the first registered <see cref="IAssetSourceReader"/> that reports it can
    /// handle the given path (via <see cref="IAssetSourceReader.CanRead"/>), or
    /// <see langword="null"/> if none is registered.
    /// </summary>
    /// <param name="path">The file path to test.</param>
    public IAssetSourceReader? FindSourceReader(string path)
    {
        foreach (var reader in _sourceReaders)
        {
            if (reader.CanRead(path))
                return reader;
        }

        return null;
    }

    /// <summary>
    /// Returns the first registered <see cref="IAssetHandler"/> that reports it can handle
    /// the given entry (via <see cref="IAssetHandler.CanHandle"/>), or
    /// <see langword="null"/> if none is registered.
    /// </summary>
    /// <param name="entry">The asset entry to evaluate.</param>
    public IAssetHandler? FindHandler(IAssetEntry entry)
    {
        foreach (var handler in _handlers)
        {
            if (handler.CanHandle(entry))
                return handler;
        }

        return null;
    }

    /// <summary>
    /// Resolves an <see cref="Asset"/> for the given entry, pairing it with the first
    /// matching <see cref="IAssetHandler"/>.
    /// </summary>
    /// <param name="entry">The entry to resolve.</param>
    /// <returns>
    /// An <see cref="Asset"/> where <see cref="Asset.Handler"/> is <see langword="null"/> when
    /// no registered handler supports the entry.
    /// </returns>
    public Asset Resolve(IAssetEntry entry) => new() { Entry = entry, Handler = FindHandler(entry) };

    // -------------------------------------------------------------------------
    // Reading
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reads and decodes the asset data for a single entry using the first matching handler.
    /// </summary>
    /// <param name="entry">The entry to read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The decoded <see cref="AssetReadResult"/>.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when no registered handler can process the entry.
    /// </exception>
    public Task<AssetReadResult> ReadAsync(IAssetEntry entry, CancellationToken cancellationToken = default)
    {
        var handler = FindHandler(entry)
            ?? throw new NotSupportedException(
                $"No registered handler supports entry '{entry.FullPath}'.");

        var context = new AssetOperationContext
        {
            AssetManager = this,
            ExportConfiguration = null,
            ExportDirectory = null,
            SkipReadIfOutputExists = false,
            ExportReferences = false,
        };

        return handler.ReadAsync(entry, context, exportDirectory: null, cancellationToken);
    }

    /// <summary>
    /// Reads and exports a single asset entry with an optional output directory override.
    /// </summary>
    /// <param name="entry">The entry to export.</param>
    /// <param name="config">The export configuration.</param>
    /// <param name="exportDirectory">Optional output root override for this entry export.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ExportEntryAsync(
        IAssetEntry entry,
        ExportConfiguration config,
        string? exportDirectory = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var handler = FindHandler(entry);
        if (handler is null)
            return;

        var effectiveDirectory = exportDirectory ?? config.OutputDirectory;

        progress?.Report($"Exporting {entry.Name}");

        var context = new AssetOperationContext
        {
            AssetManager = this,
            ExportConfiguration = config,
            ExportDirectory = effectiveDirectory,
            SkipReadIfOutputExists = config.SkipReadIfOutputExists,
            ExportReferences = config.ExportReferences,
            Flags = config.Flags,
        };

        var result = await handler.ReadAsync(entry, context, effectiveDirectory, cancellationToken).ConfigureAwait(false);

        if (result.IsSkipped)
        {
            progress?.Report($"Skipped {entry.Name}: {result.SkipReason ?? "no reason provided"}");
            return;
        }

        await handler.WriteAsync(result, context, effectiveDirectory, cancellationToken).ConfigureAwait(false);
    }

    // -------------------------------------------------------------------------
    // Exporting
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reads and exports a collection of asset entries using their respective handlers.
    /// Entries for which no handler is registered are silently skipped.
    /// </summary>
    /// <param name="entries">The entries to export.</param>
    /// <param name="config">Export configuration controlling output location and behaviour.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task ExportAsync(
        IEnumerable<IAssetEntry> entries,
        ExportConfiguration config,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var list = entries.ToList();
        var total = list.Count;

        for (var i = 0; i < total; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = list[i];
            progress?.Report($"Export queue {entry.Name} ({i + 1}/{total})");
            await ExportEntryAsync(entry, config, config.OutputDirectory, progress, cancellationToken).ConfigureAwait(false);
        }

        progress?.Report("Export complete");
    }
}
