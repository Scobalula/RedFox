using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using RedFox.GameExtraction;

namespace RedFox.GameExtraction.Template;

/// <summary>
/// Represents a mounted ZIP archive that exposes discovered assets.
/// </summary>
public sealed class ZipAssetSource : IAssetSource
{
    private readonly ZipArchive _archive;
    private readonly Stream _archiveStream;
    private readonly Dictionary<string, Asset> _assetsByPath;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZipAssetSource"/> class.
    /// </summary>
    /// <param name="name">The display name of the source.</param>
    /// <param name="archiveStream">The underlying archive stream.</param>
    /// <param name="archive">The open ZIP archive.</param>
    /// <param name="assets">The assets discovered in the archive.</param>
    public ZipAssetSource(
        string name,
        Stream archiveStream,
        ZipArchive archive,
        IReadOnlyList<Asset> assets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(archiveStream);
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(assets);

        Name = name;
        Assets = assets.ToArray();
        _archiveStream = archiveStream;
        _archive = archive;
        _assetsByPath = Assets.ToDictionary(asset => ZipPathUtility.Normalize(asset.Name), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the source display name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the assets discovered in the archive.
    /// </summary>
    public IReadOnlyList<Asset> Assets { get; }

    /// <summary>
    /// Attempts to resolve an asset by virtual path.
    /// </summary>
    /// <param name="path">The virtual path to resolve.</param>
    /// <param name="asset">The resolved asset when found.</param>
    /// <returns><see langword="true"/> when the asset exists; otherwise, <see langword="false"/>.</returns>
    public bool TryGetAsset(string path, [NotNullWhen(true)] out Asset? asset)
    {
        return _assetsByPath.TryGetValue(ZipPathUtility.Normalize(path), out asset);
    }

    /// <summary>
    /// Disposes the mounted source and closes the ZIP archive.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _archive.Dispose();
        _archiveStream.Dispose();
    }

    /// <summary>
    /// Asynchronously disposes the mounted source and closes the ZIP archive.
    /// </summary>
    /// <returns>A completed disposal task.</returns>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}