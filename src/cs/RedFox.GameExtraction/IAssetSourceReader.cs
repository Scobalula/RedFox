namespace RedFox.GameExtraction;

/// <summary>
/// Opens a game data source from file input and produces a populated <see cref="IAssetSource"/>.
/// The reader is responsible for enumerating entries; actual asset data is loaded lazily
/// by the <see cref="IAssetHandler"/> for each entry.
/// </summary>
public interface IAssetSourceReader
{
    /// <summary>
    /// Quickly determines whether this reader can attempt to open the given file path,
    /// typically by inspecting the file extension or a known magic-byte signature.
    /// Should not perform any I/O beyond reading the first few bytes when necessary.
    /// </summary>
    /// <param name="path">Absolute file path to evaluate.</param>
    /// <returns><see langword="true"/> if this reader should be attempted for the path.</returns>
    bool CanRead(string path);

    /// <summary>
    /// Opens and enumerates the asset source from the provided stream.
    /// VFS-aware readers should call <see cref="AssetManager.EnsureFileSystem"/> and mount
    /// files into it; VFS-unaware readers may ignore <paramref name="assetManager"/> beyond
    /// handler/reader lookups.
    /// </summary>
    /// <param name="stream">Readable stream positioned at the start of the source data.</param>
    /// <param name="path">Absolute path of the source file being opened.</param>
    /// <param name="assetManager">The owning asset manager.</param>
    /// <param name="progress">Optional progress reporter for the enumeration phase.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A populated <see cref="IAssetSource"/> ready for use.</returns>
    Task<IAssetSource> ReadAsync(
        Stream stream,
        string path,
        AssetManager assetManager,
        IProgress<string>? progress,
        CancellationToken cancellationToken);
}
