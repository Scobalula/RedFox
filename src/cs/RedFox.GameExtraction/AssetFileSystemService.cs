using RedFox.IO.FileSystem;

namespace RedFox.GameExtraction;

/// <summary>
/// Optional service that provides a shared virtual file system for readers
/// that expose archive contents through <see cref="VirtualFile"/> instances.
/// </summary>
public sealed class AssetFileSystemService
{
    /// <summary>
    /// Gets or sets the asset manager responsible for handling asset operations.
    /// </summary>
    public AssetManager Manager { get; set; }

    /// <summary>
    /// Gets the shared virtual file system.
    /// </summary>
    public VirtualFileSystem FileSystem { get; } = new();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="manager"></param>
    public AssetFileSystemService(AssetManager manager)
    {
        Manager = manager;
        Manager.SourceUnloaded += ManagerSourceMounted;
    }

    private void ManagerSourceMounted(object? sender, SourceEventArgs e)
    {
        foreach (var asset in e.Source.Assets)
        {
            if (asset.DataSource is VirtualFile file)
            {
                file.MoveTo(null);
            }
        }
    }
}
