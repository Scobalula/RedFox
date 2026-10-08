// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Diagnostics.CodeAnalysis;

namespace RedFox.IO.FileSystem;

/// <summary>
/// An in-memory file system that can hold files from any number of sources, such as directories on disk or archives.
/// </summary>
public sealed class VirtualFileSystem
{
    internal static readonly char[] Separators = ['/', '\\'];

    private readonly Lock _lock = new();

    /// <summary>
    /// Gets the root directory.
    /// </summary>
    public VirtualDirectory Root { get; }

    /// <summary>
    /// Initializes a new, empty <see cref="VirtualFileSystem"/>.
    /// </summary>
    public VirtualFileSystem()
    {
        Root = new(null, string.Empty, _lock);
    }

    /// <summary>
    /// Creates all directories in the given path unless they already exist.
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>The directory at the end of the path.</returns>
    public VirtualDirectory CreateDirectory(string path)
    {
        lock (_lock)
            return CreateDirectories(path.Split(Separators, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Adds a file to the given path, creating its parent directories. Throws if the path is already occupied.
    /// </summary>
    /// <param name="path">The full path of the file, including its name.</param>
    /// <param name="file">The file to add.</param>
    /// <returns><see langword="true"/> once the file has been added.</returns>
    /// <exception cref="IOException">A file already exists at the given path.</exception>
    public bool AddFile(string path, VirtualFile file) => AddFile(path, file, FileCollisionBehavior.Throw);

    /// <summary>
    /// Adds a file to the given path, creating its parent directories.
    /// </summary>
    /// <param name="path">The full path of the file, including its name.</param>
    /// <param name="file">The file to add.</param>
    /// <param name="behavior">How to handle a file that already exists at the given path.</param>
    /// <returns><see langword="true"/> if the file was added; <see langword="false"/> if it was skipped.</returns>
    /// <exception cref="IOException">A file already exists at the given path and <paramref name="behavior"/> is <see cref="FileCollisionBehavior.Throw"/>.</exception>
    public bool AddFile(string path, VirtualFile file, FileCollisionBehavior behavior)
    {
        if (file.Parent is not null)
            throw new InvalidOperationException($"File has already been added: {file.FullPath}");

        var names = path.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

        if (names.Length == 0)
            throw new ArgumentException("Path does not contain a file name.", nameof(path));

        var name = names[^1];
        ValidateName(name);

        lock (_lock)
        {
            var directory = CreateDirectories(names.AsSpan()[..^1]);

            if (directory._files.TryGetValue(name, out var existing))
            {
                if (behavior == FileCollisionBehavior.Throw)
                    throw new IOException($"File already exists: {existing.FullPath}");
                if (behavior == FileCollisionBehavior.KeepExisting)
                    return false;

                file.Shadowed = existing;
            }

            file.Name = name;
            file.Parent = directory;
            directory._files[name] = file;

            return true;
        }
    }

    /// <summary>
    /// Removes a file. If the file shadows another file, the shadowed file becomes visible again.
    /// </summary>
    /// <param name="file">The file to remove.</param>
    /// <returns><see langword="true"/> if the file was removed; <see langword="false"/> if it was not in a file system.</returns>
    public bool RemoveFile(VirtualFile file)
    {
        lock (_lock)
        {
            if (file.Parent is not { } directory)
                return false;

            var visible = directory._files[file.Name];

            if (visible == file)
            {
                if (file.Shadowed is null)
                    directory._files.Remove(file.Name);
                else
                    directory._files[file.Name] = file.Shadowed;
            }
            else
            {
                while (visible.Shadowed != file)
                    visible = visible.Shadowed!;

                visible.Shadowed = file.Shadowed;
            }

            file.Parent = null;
            file.Shadowed = null;

            return true;
        }
    }

    /// <summary>
    /// Removes an empty directory.
    /// </summary>
    /// <param name="directory">The directory to remove.</param>
    /// <returns><see langword="true"/> if the directory was removed; <see langword="false"/> if it is the root, already removed, or not empty.</returns>
    public bool RemoveDirectory(VirtualDirectory directory)
    {
        lock (_lock)
        {
            if (directory.Parent is not { } parent || directory._files.Count > 0 || directory._directories.Count > 0)
                return false;

            parent._directories.Remove(directory.Name);
            directory.Parent = null;

            return true;
        }
    }

    /// <summary>
    /// Gets a file by its path.
    /// </summary>
    /// <param name="path">The path relative to the root directory.</param>
    /// <returns>The matching file.</returns>
    /// <exception cref="FileNotFoundException">The path does not identify a file.</exception>
    public VirtualFile GetFile(string path) => Root.GetFile(path);

    /// <summary>
    /// Attempts to get a file by its path.
    /// </summary>
    /// <param name="path">The path relative to the root directory.</param>
    /// <param name="file">Receives the matching file when found.</param>
    /// <returns><see langword="true"/> if the file exists; otherwise, <see langword="false"/>.</returns>
    public bool TryGetFile(string path, [NotNullWhen(true)] out VirtualFile? file) => Root.TryGetFile(path, out file);

    /// <summary>
    /// Attempts to get a file of the requested type by its path.
    /// </summary>
    /// <typeparam name="T">The file type to find.</typeparam>
    /// <param name="path">The path relative to the root directory.</param>
    /// <param name="file">Receives the matching file when found.</param>
    /// <returns><see langword="true"/> if a matching file exists; otherwise, <see langword="false"/>.</returns>
    public bool TryGetFile<T>(string path, [NotNullWhen(true)] out T? file) where T : VirtualFile => Root.TryGetFile(path, out file);

    /// <summary>
    /// Attempts to get the first file whose name matches a search pattern.
    /// Only the final path segment is treated as a pattern; the directory path must be exact.
    /// </summary>
    /// <param name="path">The path relative to the root directory. The final segment is a search pattern.</param>
    /// <param name="file">Receives the first matching file when found.</param>
    /// <returns><see langword="true"/> if a matching file exists; otherwise, <see langword="false"/>.</returns>
    public bool TryFindFirstFile(string path, [NotNullWhen(true)] out VirtualFile? file) => Root.TryFindFirstFile(path, out file);

    /// <summary>
    /// Attempts to get the first file of the requested type whose name matches a search pattern.
    /// Only the final path segment is treated as a pattern; the directory path must be exact.
    /// </summary>
    /// <typeparam name="T">The file type to find.</typeparam>
    /// <param name="path">The path relative to the root directory. The final segment is a search pattern.</param>
    /// <param name="file">Receives the first matching file when found.</param>
    /// <returns><see langword="true"/> if a matching file exists; otherwise, <see langword="false"/>.</returns>
    public bool TryFindFirstFile<T>(string path, [NotNullWhen(true)] out T? file) where T : VirtualFile => Root.TryFindFirstFile(path, out file);

    /// <summary>
    /// Gets a directory by its path.
    /// </summary>
    /// <param name="path">The path relative to the root directory.</param>
    /// <returns>The matching directory.</returns>
    /// <exception cref="DirectoryNotFoundException">The path does not identify a directory.</exception>
    public VirtualDirectory GetDirectory(string path) => Root.GetDirectory(path);

    /// <summary>
    /// Attempts to get a directory by its path.
    /// </summary>
    /// <param name="path">The path relative to the root directory.</param>
    /// <param name="directory">Receives the matching directory when found.</param>
    /// <returns><see langword="true"/> if the directory exists; otherwise, <see langword="false"/>.</returns>
    public bool TryGetDirectory(string path, [NotNullWhen(true)] out VirtualDirectory? directory) => Root.TryGetDirectory(path, out directory);

    private VirtualDirectory CreateDirectories(ReadOnlySpan<string> names)
    {
        foreach (var name in names)
            ValidateName(name);

        var directory = Root;

        foreach (var name in names)
        {
            if (!directory._directories.TryGetValue(name, out var child))
            {
                child = new VirtualDirectory(directory, name, _lock);
                directory._directories.Add(name, child);
            }

            directory = child;
        }

        return directory;
    }

    private static void ValidateName(string name)
    {
        if (name is "." or "..")
            throw new ArgumentException($"Invalid name: {name}", nameof(name));
    }
}
