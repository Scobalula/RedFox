using RedFox.IO.FileSystem;

namespace RedFox.GameExtraction;

/// <summary>
/// Optional service that provides a shared virtual file system for readers
/// that expose archive contents through <see cref="VirtualFile"/> instances.
/// </summary>
public sealed class AssetFileSystemService
{
    private readonly object _fileSystemLock = new();

    /// <summary>
    /// Gets or sets the asset manager responsible for handling asset operations.
    /// </summary>
    public AssetManager Manager { get; set; }

    /// <summary>
    /// Gets the shared virtual file system.
    /// </summary>
    public VirtualFileSystem FileSystem { get; } = new();

    /// <summary>
    /// Creates a shared virtual file system for the supplied asset manager.
    /// </summary>
    /// <param name="manager">The asset manager whose mounted files to expose.</param>
    public AssetFileSystemService(AssetManager manager)
    {
        Manager = manager;
        Manager.SourceMounted += ManagerSourceMounted;
        Manager.SourceUnloading += ManagerSourceUnloading;
    }

    private void ManagerSourceMounted(object? sender, SourceEventArgs e)
    {
        List<VirtualFile> addedFiles = [];

        lock (_fileSystemLock)
        {
            try
            {
                foreach (Asset asset in e.Source.Assets)
                {
                    if (asset.DataSource is not VirtualFile file)
                        continue;

                    FileSystem.AddFile(AssetManager.NormalizeVirtualPath(asset.Name), file);
                    addedFiles.Add(file);
                }
            }
            catch
            {
                foreach (VirtualFile file in addedFiles)
                    file.MoveTo(null);

                throw;
            }
        }
    }

    private void ManagerSourceUnloading(object? sender, SourceEventArgs e)
    {
        lock (_fileSystemLock)
        {
            HashSet<VirtualDirectory> topLevelDirectories = new(ReferenceEqualityComparer.Instance);

            foreach (Asset asset in e.Source.Assets)
            {
                if (asset.DataSource is not VirtualFile file)
                    continue;

                if (file.Parent is not null && file.Parent != FileSystem.Root)
                    topLevelDirectories.Add(GetTopLevelDirectory(file.Parent));

                file.MoveTo(null);
            }

            foreach (VirtualDirectory directory in topLevelDirectories)
                RemoveEmptyDirectories(directory);
        }
    }

    private static VirtualDirectory GetTopLevelDirectory(VirtualDirectory directory)
    {
        while (directory.Parent?.Parent is not null)
            directory = directory.Parent;

        return directory;
    }

    private static void RemoveEmptyDirectories(VirtualDirectory directory)
    {
        foreach (VirtualDirectory child in directory.Directories.ToArray())
            RemoveEmptyDirectories(child);

        if (directory.Files.Count == 0 && directory.Directories.Count == 0)
            directory.MoveTo(null);
    }
}
