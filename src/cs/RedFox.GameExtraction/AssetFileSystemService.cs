using RedFox.IO.FileSystem;

namespace RedFox.GameExtraction;

/// <summary>
/// Optional service that provides a shared virtual file system for readers
/// that expose archive contents through <see cref="VirtualFile"/> instances.
/// </summary>
public sealed class AssetFileSystemService
{
    /// <summary>
    /// Gets the shared virtual file system.
    /// </summary>
    public VirtualFileSystem FileSystem { get; } = new();
}
