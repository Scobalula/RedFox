using RedFox.IO.FileSystem;

namespace RedFox.GameExtraction;

/// <summary>
/// Optional service that provides a shared virtual file system and removes the files and directories
/// a source reader added through it when that source is unloaded or fails to mount.
/// </summary>
public sealed class AssetFileSystemService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<IAssetSource, AssetFileSystemEntries> _sourceEntries = new(ReferenceEqualityComparer.Instance);
    private AssetFileSystemEntries _pendingEntries = new();

    /// <summary>
    /// Gets the asset manager responsible for handling asset operations.
    /// </summary>
    public AssetManager Manager { get; }

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
        Manager.SourceMountFailed += ManagerSourceMountFailed;
        Manager.SourceUnloading += ManagerSourceUnloading;
    }

    /// <summary>
    /// Creates all directories in the given path for the source currently being mounted.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>The directory at the end of the path.</returns>
    public VirtualDirectory CreateDirectory(string path)
    {
        var directory = FileSystem.CreateDirectory(path);

        lock (_lock)
            _pendingEntries.TopLevelDirectories.Add(GetTopLevelDirectory(directory));

        return directory;
    }

    /// <summary>
    /// Adds a file for the source currently being mounted. Throws if the path is already occupied.
    /// </summary>
    /// <param name="path">The full path of the file, including its name.</param>
    /// <param name="file">The file to add.</param>
    /// <returns><see langword="true"/> once the file has been added.</returns>
    public bool AddFile(string path, VirtualFile file) => AddFile(path, file, FileCollisionBehavior.Throw);

    /// <summary>
    /// Adds a file for the source currently being mounted.
    /// </summary>
    /// <param name="path">The full path of the file, including its name.</param>
    /// <param name="file">The file to add.</param>
    /// <param name="behavior">How to handle a file that already exists at the given path.</param>
    /// <returns><see langword="true"/> if the file was added; <see langword="false"/> if it was skipped.</returns>
    public bool AddFile(string path, VirtualFile file, FileCollisionBehavior behavior)
    {
        if (!FileSystem.AddFile(path, file, behavior))
            return false;

        lock (_lock)
        {
            _pendingEntries.Files.Add(file);
            _pendingEntries.TopLevelDirectories.Add(GetTopLevelDirectory(file.Parent!));
        }

        return true;
    }

    private void ManagerSourceMounted(object? sender, SourceEventArgs e)
    {
        lock (_lock)
        {
            _sourceEntries[e.Source] = _pendingEntries;
            _pendingEntries = new();
        }
    }

    private void ManagerSourceMountFailed(object? sender, AssetOperationFailedEventArgs e)
    {
        lock (_lock)
        {
            RemoveEntries(_pendingEntries);
            _pendingEntries = new();

            if (e.Source is not null && _sourceEntries.Remove(e.Source, out var entries))
                RemoveEntries(entries);
        }
    }

    private void ManagerSourceUnloading(object? sender, SourceEventArgs e)
    {
        lock (_lock)
        {
            if (_sourceEntries.Remove(e.Source, out var entries))
                RemoveEntries(entries);
        }
    }

    private void RemoveEntries(AssetFileSystemEntries entries)
    {
        foreach (VirtualFile file in entries.Files)
            FileSystem.RemoveFile(file);

        foreach (VirtualDirectory directory in entries.TopLevelDirectories)
        {
            if (directory != FileSystem.Root)
                RemoveEmptyDirectories(directory);
        }
    }

    private void RemoveEmptyDirectories(VirtualDirectory directory)
    {
        foreach (VirtualDirectory child in directory.Directories)
            RemoveEmptyDirectories(child);

        FileSystem.RemoveDirectory(directory);
    }

    private static VirtualDirectory GetTopLevelDirectory(VirtualDirectory directory)
    {
        while (directory.Parent?.Parent is not null)
            directory = directory.Parent;

        return directory;
    }
}
