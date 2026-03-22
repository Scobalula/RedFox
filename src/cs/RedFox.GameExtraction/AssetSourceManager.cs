namespace RedFox.GameExtraction;

/// <summary>
/// Tracks registered file readers and the sources they load.
/// </summary>
/// <typeparam name="TAsset">The asset type.</typeparam>
public sealed class AssetSourceManager<TAsset> : IDisposable
{
    private readonly List<IFileAssetSourceReader<TAsset>> _fileReaders = [];
    private readonly List<IAssetSource> _sources = [];

    /// <summary>
    /// Gets the currently tracked sources.
    /// </summary>
    public IReadOnlyList<IAssetSource> Sources => _sources;

    /// <summary>
    /// Registers a file reader.
    /// </summary>
    /// <param name="reader">The reader to register.</param>
    public void RegisterFileReader(IFileAssetSourceReader<TAsset> reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        if (string.IsNullOrWhiteSpace(reader.FileFilter))
            throw new ArgumentException("File readers must declare a file filter.", nameof(reader));
        if (reader.Extensions.Count == 0)
            throw new ArgumentException("File readers must declare at least one extension.", nameof(reader));

        foreach (var extension in reader.Extensions)
        {
            if (string.IsNullOrWhiteSpace(extension) || extension[0] != '.')
                throw new ArgumentException("File reader extensions must start with '.'.", nameof(reader));

            if (_fileReaders.Any(existing => existing.Extensions.Any(candidate => candidate.Equals(extension, StringComparison.OrdinalIgnoreCase))))
                throw new InvalidOperationException($"A file reader is already registered for: {extension}");
        }

        _fileReaders.Add(reader);
    }

    /// <summary>
    /// Builds a combined file filter string from all registered readers.
    /// </summary>
    public string BuildFileFilter()
    {
        return string.Join("|", _fileReaders.Select(reader => reader.FileFilter));
    }

    /// <summary>
    /// Loads assets from a file using the registered reader that matches its extension.
    /// </summary>
    /// <param name="filePath">The file path to load.</param>
    /// <param name="progress">Optional progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded source and discovered assets.</returns>
    public AssetSourceLoadResult<TAsset> LoadFromFile(string filePath, IProgress<(int Current, int Total, string Status)>? progress = null, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(filePath);
        var reader = _fileReaders.FirstOrDefault(candidate => candidate.Extensions.Any(candidateExtension => candidateExtension.Equals(extension, StringComparison.OrdinalIgnoreCase))) ?? throw new NotSupportedException($"No file reader registered for: {extension}");
        var result = reader.LoadFromFile(filePath, progress, cancellationToken);

        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(result.Source);
        ArgumentNullException.ThrowIfNull(result.Assets);

        _sources.Add(result.Source);
        return result;
    }

    /// <summary>
    /// Removes and disposes a tracked source.
    /// </summary>
    /// <param name="sourceId">The source identifier.</param>
    /// <returns><c>true</c> if the source was found and removed.</returns>
    public bool RemoveSource(Guid sourceId)
    {
        var source = _sources.FirstOrDefault(candidate => candidate.Id == sourceId);
        if (source is null)
            return false;

        _sources.Remove(source);
        source.Dispose();
        return true;
    }

    /// <summary>
    /// Disposes and removes all tracked sources.
    /// </summary>
    public void ClearSources()
    {
        foreach (var source in _sources)
            source.Dispose();

        _sources.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ClearSources();
    }
}