using System.IO.Compression;
using RedFox.IO.FileSystem;

namespace RedFox.GameExtraction.Template.Cli;

/// <summary>
/// Exposes a ZIP archive entry through the shared virtual file system.
/// </summary>
public sealed class ZipVirtualFile : VirtualFile
{
    private readonly ZipArchiveEntry _entry;

    /// <summary>
    /// Initializes a new virtual file wrapper for the supplied ZIP entry.
    /// </summary>
    /// <param name="entry">The ZIP entry to expose through the VFS.</param>
    public ZipVirtualFile(ZipArchiveEntry entry) : base(entry?.Name ?? throw new ArgumentNullException(nameof(entry)), entry.Length)
    {
        _entry = entry;
    }

    /// <summary>
    /// Opens a readable stream for the wrapped ZIP entry.
    /// </summary>
    /// <returns>A readable stream for the entry payload.</returns>
    public override Stream Open()
    {
        return _entry.Open();
    }
}
