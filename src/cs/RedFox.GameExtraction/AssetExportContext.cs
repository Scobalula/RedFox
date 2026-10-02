namespace RedFox.GameExtraction;

/// <summary>
/// Carries contextual information for asset export operations.
/// </summary>
public sealed class AssetExportContext
{
    private readonly IProgress<string>? _progress;
    private readonly CancellationToken _cancellationToken;

    /// <summary>
    /// Gets the manager coordinating the export.
    /// </summary>
    public AssetManager AssetManager { get; }

    /// <summary>
    /// Gets the source that owns the asset being exported.
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
    /// Gets the shared settings and options used for the operation.
    /// </summary>
    public GameExtractionConfiguration Configuration { get; }

    /// <summary>
    /// Gets the setting values and additional options associated with the operation.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Options => Configuration.Options;

    /// <summary>
    /// Gets the relative output directory applied to the current export scope.
    /// </summary>
    public string RelativeOutputDirectory { get; }

    /// <summary>
    /// Gets the absolute output directory for the current export scope.
    /// </summary>
    public string OutputDirectory => ResolveOutputDirectory();

    /// <summary>
    /// Gets optional user-defined data attached to the export operation.
    /// </summary>
    public object? UserData { get; }

    /// <summary>Gets the cancellation token for this export operation.</summary>
    public CancellationToken CancellationToken => _cancellationToken;

    internal AssetExportContext(AssetManager assetManager, IAssetSource source, AssetSourceRequest request, GameExtractionConfiguration configuration, string relativeOutputDirectory, IProgress<string>? progress, CancellationToken cancellationToken)
        : this(assetManager, source, request, configuration, relativeOutputDirectory, progress, cancellationToken, null)
    {
    }

    internal AssetExportContext(AssetManager assetManager, IAssetSource source, AssetSourceRequest request, GameExtractionConfiguration configuration, string relativeOutputDirectory, IProgress<string>? progress, CancellationToken cancellationToken, object? userData)
    {
        AssetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        RelativeOutputDirectory = AssetManager.NormalizeRelativeOutputDirectory(relativeOutputDirectory);
        _progress = progress;
        _cancellationToken = cancellationToken;
        UserData = userData;
    }

    /// <summary>
    /// Resolves the default output directory for an asset.
    /// </summary>
    /// <param name="asset">The asset being exported.</param>
    /// <returns>The resolved absolute output directory.</returns>
    public string ResolveAssetDirectory(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        string relativeDirectory = Configuration.GetOption("PreserveDirectoryStructure", true) ? Path.GetDirectoryName(global::RedFox.GameExtraction.AssetManager.NormalizeVirtualPath(asset.Name)) ?? string.Empty : string.Empty;

        return ResolveOutputDirectory(relativeDirectory);
    }

    /// <summary>
    /// Resolves the default output path for an asset using its original file name.
    /// </summary>
    /// <param name="asset">The asset being exported.</param>
    /// <returns>The resolved absolute output path.</returns>
    public string ResolveAssetPath(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);

        string relativePath = Configuration.GetOption("PreserveDirectoryStructure", true) ? global::RedFox.GameExtraction.AssetManager.NormalizeVirtualPath(asset.Name) : Path.GetFileName(asset.Name);

        return ResolveOutputPath(relativePath);
    }

    /// <summary>
    /// Resolves the default output path for an asset using an alternate extension.
    /// </summary>
    /// <param name="asset">The asset being exported.</param>
    /// <param name="extension">The extension to apply to the output file.</param>
    /// <returns>The resolved absolute output path.</returns>
    public string ResolveAssetPath(Asset asset, string extension)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);

        string fileName = Path.GetFileNameWithoutExtension(asset.Name)
            + (extension.StartsWith('.') ? extension : $".{extension}");
        string relativeDirectory = Configuration.GetOption("PreserveDirectoryStructure", true) ? Path.GetDirectoryName(global::RedFox.GameExtraction.AssetManager.NormalizeVirtualPath(asset.Name)) ?? string.Empty : string.Empty;
        string relativePath = string.IsNullOrWhiteSpace(relativeDirectory) ? fileName : Path.Combine(relativeDirectory, fileName);

        return ResolveOutputPath(relativePath);
    }

    /// <summary>
    /// Resolves an absolute output path from a relative path within the current export scope.
    /// </summary>
    /// <param name="relativePath">The relative path to resolve.</param>
    /// <returns>The resolved absolute output path.</returns>
    public string ResolveOutputPath(string relativePath)
    {
        string outputRoot = Path.GetFullPath(Configuration.GetOption("OutputDirectory", GameExtractionSettings.GetDefaultOutputDirectory()));
        string outputPath = Path.GetFullPath(Path.Combine(OutputDirectory, AssetManager.NormalizeRelativeOutputDirectory(relativePath)));
        EnsurePathIsWithinRoot(outputRoot, outputPath);
        return outputPath;
    }

    /// <summary>
    /// Resolves an absolute output directory from a relative path within the current export scope.
    /// </summary>
    /// <returns>The resolved absolute output directory.</returns>
    public string ResolveOutputDirectory() => ResolveOutputDirectory(string.Empty);

    /// <summary>
    /// Resolves an absolute output directory from a relative path within the current export scope.
    /// </summary>
    /// <param name="relativePath">The relative directory path to resolve.</param>
    /// <returns>The resolved absolute output directory.</returns>
    public string ResolveOutputDirectory(string relativePath)
    {
        string outputRoot = Path.GetFullPath(Configuration.GetOption("OutputDirectory", GameExtractionSettings.GetDefaultOutputDirectory()));
        string combinedRelativePath = AssetManager.CombineRelativePaths(RelativeOutputDirectory, relativePath);
        string outputPath = string.IsNullOrWhiteSpace(combinedRelativePath) ? outputRoot : Path.GetFullPath(Path.Combine(outputRoot, combinedRelativePath));
        EnsurePathIsWithinRoot(outputRoot, outputPath);
        return outputPath;
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
    /// Attempts to resolve a typed source option.
    /// </summary>
    /// <typeparam name="T">The option type.</typeparam>
    /// <param name="key">The option key.</param>
    /// <param name="value">The resolved option value when available.</param>
    /// <returns><see langword="true"/> when the option is present and of the requested type; otherwise, <see langword="false"/>.</returns>
    public bool TryGetSourceOption<T>(string key, out T? value) => TryGetOption(SourceOptions, key, out value);

    /// <summary>
    /// Attempts to resolve a typed setting value or configuration option.
    /// </summary>
    /// <typeparam name="T">The option type.</typeparam>
    /// <param name="key">The option key.</param>
    /// <param name="value">The resolved option value when available.</param>
    /// <returns><see langword="true"/> when the option is present and of the requested type; otherwise, <see langword="false"/>.</returns>
    public bool TryGetConfigurationOption<T>(string key, out T? value) => TryGetOption(Options, key, out value);

    /// <summary>
    /// Recursively exports another asset as part of the current export batch, placed within the current scope's output directory.
    /// </summary>
    /// <param name="asset">The asset to export.</param>
    public Task ExportAsync(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return AssetManager.ExportAsync(asset, RelativeOutputDirectory, Configuration, _progress, _cancellationToken);
    }

    /// <summary>
    /// Recursively exports another asset into a sub-directory of the current export scope.
    /// </summary>
    /// <param name="asset">The asset to export.</param>
    /// <param name="relativeOutputDirectory">The relative directory under the current scope to place the asset in.</param>
    public Task ExportAsync(Asset asset, string relativeOutputDirectory)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return AssetManager.ExportAsync(asset, AssetManager.CombineRelativePaths(RelativeOutputDirectory, relativeOutputDirectory), Configuration, _progress, _cancellationToken);
    }

    /// <summary>
    /// Recursively exports another asset using a pre-computed read result, skipping the manager's read step.
    /// </summary>
    /// <param name="asset">The asset to export.</param>
    /// <param name="result">A read result whose <see cref="AssetReadResult.Asset"/> matches <paramref name="asset"/>.</param>
    public Task ExportAsync(Asset asset, AssetReadResult result)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(result);
        if (!ReferenceEquals(result.Asset, asset))
            throw new ArgumentException($"The result belongs to '{result.Asset.Name}' but the asset being exported is '{asset.Name}'.", nameof(result));
        return AssetManager.ExportAsync(asset, result, RelativeOutputDirectory, Configuration, _progress, _cancellationToken);
    }

    /// <summary>
    /// Recursively exports another asset into a sub-directory of the current scope using a pre-computed read result.
    /// </summary>
    /// <param name="asset">The asset to export.</param>
    /// <param name="result">A read result whose <see cref="AssetReadResult.Asset"/> matches <paramref name="asset"/>.</param>
    /// <param name="relativeOutputDirectory">The relative directory under the current scope to place the asset in.</param>
    public Task ExportAsync(Asset asset, AssetReadResult result, string relativeOutputDirectory)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(result);
        if (!ReferenceEquals(result.Asset, asset))
            throw new ArgumentException($"The result belongs to '{result.Asset.Name}' but the asset being exported is '{asset.Name}'.", nameof(result));
        return AssetManager.ExportAsync(asset, result, AssetManager.CombineRelativePaths(RelativeOutputDirectory, relativeOutputDirectory), Configuration, _progress, _cancellationToken);
    }

    /// <summary>
    /// Recursively exports an asset identified by its read result as part of the current export batch, placed within the current scope's output directory.
    /// </summary>
    /// <param name="result">The read result to export. The asset is taken from <see cref="AssetReadResult.Asset"/>.</param>
    public Task ExportAsync(AssetReadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return AssetManager.ExportAsync(result, RelativeOutputDirectory, Configuration, _progress, _cancellationToken);
    }

    /// <summary>
    /// Recursively exports an asset identified by its read result into a sub-directory of the current export scope.
    /// </summary>
    /// <param name="result">The read result to export. The asset is taken from <see cref="AssetReadResult.Asset"/>.</param>
    /// <param name="relativeOutputDirectory">The relative directory under the current scope to place the asset in.</param>
    public Task ExportAsync(AssetReadResult result, string relativeOutputDirectory)
    {
        ArgumentNullException.ThrowIfNull(result);
        return AssetManager.ExportAsync(result, AssetManager.CombineRelativePaths(RelativeOutputDirectory, relativeOutputDirectory), Configuration, _progress, _cancellationToken);
    }

    private static void EnsurePathIsWithinRoot(string root, string path)
    {
        string relativePath = Path.GetRelativePath(root, path);
        if (Path.IsPathRooted(relativePath) || relativePath == ".." || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            throw new ArgumentException("The resolved output path must stay within the output directory.", nameof(path));
    }

    private static bool TryGetOption<T>(IReadOnlyDictionary<string, object?> options, string key, out T? value)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (options.TryGetValue(key, out object? rawValue) && rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }
}
