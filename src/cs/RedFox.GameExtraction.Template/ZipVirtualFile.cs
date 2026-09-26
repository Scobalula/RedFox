using System.IO.Compression;
using RedFox.IO.FileSystem;

namespace RedFox.GameExtraction.Template;

/// <summary>
/// Exposes a ZIP archive entry through the shared virtual file system.
/// </summary>
public sealed class ZipVirtualFile : VirtualFile
{
    private readonly ZipArchiveEntry _entry;
    private readonly SemaphoreSlim _archiveLock;

    internal ZipVirtualFile(ZipArchiveEntry entry, SemaphoreSlim archiveLock)
        : base(entry?.Name ?? throw new ArgumentNullException(nameof(entry)), entry.Length)
    {
        _entry = entry;
        _archiveLock = archiveLock ?? throw new ArgumentNullException(nameof(archiveLock));
    }

    /// <summary>
    /// Opens a readable stream for the wrapped ZIP entry.
    /// </summary>
    /// <returns>A readable stream for the entry payload.</returns>
    public override Stream Open()
    {
        _archiveLock.Wait();
        try
        {
            return new ZipEntryStream(_entry.Open(), _archiveLock);
        }
        catch
        {
            _archiveLock.Release();
            throw;
        }
    }
}
