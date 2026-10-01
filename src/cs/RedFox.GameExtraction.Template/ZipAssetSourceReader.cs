using System.IO.Compression;
using System.Runtime.InteropServices;
using RedFox.GameExtraction;

namespace RedFox.GameExtraction.Template;

/// <summary>
/// Opens standard ZIP archives as <see cref="IAssetSource"/> instances.
/// </summary>
public sealed class ZipAssetSourceReader : IAssetSourceReader
{
    /// <summary>
    /// Determines whether the supplied request appears to target a ZIP file.
    /// </summary>
    /// <param name="request">The source request to inspect.</param>
    /// <returns><see langword="true"/> when the request appears to target a ZIP file; otherwise, <see langword="false"/>.</returns>
    public bool CanOpen(AssetSourceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Kind != AssetSourceKind.File)
        {
            return false;
        }

        if (request.Header.Length >= sizeof(uint) && MemoryMarshal.Read<uint>(request.HeaderSpan) is uint signature && (signature == 0x04034B50u || signature == 0x06054B50u || signature == 0x08074B50u))
        {
            return true;
        }

        return string.Equals(Path.GetExtension(request.Location), ".zip", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Opens the ZIP archive and registers its entries as assets and virtual files.
    /// </summary>
    /// <param name="request">The file request to open.</param>
    /// <param name="assetManager">The manager coordinating the mount.</param>
    /// <param name="progress">Optional progress reporter for mount messages.</param>
    /// <param name="cancellationToken">The cancellation token for the mount operation.</param>
    /// <returns>A mounted ZIP-backed asset source.</returns>
    public Task<IAssetSource> OpenAsync(AssetSourceRequest request, AssetManager assetManager, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(assetManager);

        string location = request.Location
            ?? throw new InvalidOperationException("ZIP requests must provide a file location.");

        if (!File.Exists(location))
        {
            throw new FileNotFoundException("ZIP file was not found.", location);
        }

        FileStream stream = File.Open(location, FileMode.Open, FileAccess.Read, FileShare.Read);
        ZipArchive? archive = null;
        SemaphoreSlim archiveLock = new(1, 1);

        try
        {
            archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            List<Asset> assets = [];
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                string assetPath = ZipPathUtility.Normalize(entry.FullName);
                ZipVirtualFile file = new(entry, archiveLock);
                Asset asset = new(assetPath, ZipPathUtility.GetAssetType(entry.Name), file, $"{entry.Length:N0} bytes", new Dictionary<string, object?> { ["ArchivePath"] = location, }, null, ZipPathUtility.GetAssetCategory(entry.Name));

                assets.Add(asset);
                if (assets.Count % 128 == 0)
                    progress?.Report($"Mounted {asset.Name}");
            }

            if (assets.Count > 0 && assets.Count % 128 != 0)
                progress?.Report($"Mounted {assets[^1].Name}");

            IAssetSource source = new ZipAssetSource(request.DisplayName, stream, archive, assets, archiveLock);
            return Task.FromResult(source);
        }
        catch
        {
            archive?.Dispose();
            stream.Dispose();
            archiveLock.Dispose();
            throw;
        }
    }
}
