namespace RedFox.GameExtraction;

/// <summary>
/// Loads assets from one or more file extensions into a managed source.
/// </summary>
/// <typeparam name="TAsset">The asset type.</typeparam>
public interface IFileAssetSourceReader<TAsset>
{
    /// <summary>
    /// Gets the file extensions supported by this reader.
    /// </summary>
    IReadOnlyList<string> Extensions { get; }

    /// <summary>
    /// Gets the file filter string used by open-file dialogs.
    /// </summary>
    string FileFilter { get; }

    /// <summary>
    /// Loads assets from the specified file path.
    /// </summary>
    /// <param name="filePath">The file path to inspect.</param>
    /// <param name="progress">Optional progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The loaded source and discovered assets.</returns>
    AssetSourceLoadResult<TAsset> LoadFromFile(string filePath, IProgress<(int Current, int Total, string Status)>? progress = null, CancellationToken cancellationToken = default);
}