// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System.Diagnostics.CodeAnalysis;
using System.IO.Enumeration;

namespace RedFox.IO.FileSystem;

/// <summary>
/// A directory within a <see cref="VirtualFileSystem"/> that contains files and sub-directories.
/// </summary>
public sealed class VirtualDirectory
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    internal readonly Lock _syncRoot;

    internal readonly Dictionary<string, VirtualDirectory> _directories = new(StringComparer.OrdinalIgnoreCase);

    internal readonly Dictionary<string, VirtualFile> _files = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the parent directory, or <see langword="null"/> for the root directory or a removed directory.
    /// </summary>
    public VirtualDirectory? Parent { get; internal set; }

    /// <summary>
    /// Gets the name of the directory.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the full path of the directory, using '/' as the separator.
    /// </summary>
    public string FullPath => Parent is null || Parent.Parent is null ? Name : $"{Parent.FullPath}/{Name}";

    /// <summary>
    /// Gets a snapshot of the directories stored within this directory.
    /// </summary>
    public IReadOnlyCollection<VirtualDirectory> Directories
    {
        get
        {
            lock (_syncRoot)
                return [.. _directories.Values];
        }
    }

    /// <summary>
    /// Gets a snapshot of the visible files stored within this directory.
    /// </summary>
    public IReadOnlyCollection<VirtualFile> Files
    {
        get
        {
            lock (_syncRoot)
                return [.. _files.Values];
        }
    }

    internal VirtualDirectory(VirtualDirectory? parent, string name, Lock syncRoot)
    {
        Parent = parent;
        Name = name;
        _syncRoot = syncRoot;
    }

    /// <summary>
    /// Returns the directories directly within this directory.
    /// </summary>
    /// <returns>An enumerable collection of directories.</returns>
    public IEnumerable<VirtualDirectory> EnumerateDirectories() => EnumerateDirectories("*", SearchOption.TopDirectoryOnly);

    /// <summary>
    /// Returns the directories directly within this directory that match a search pattern.
    /// </summary>
    /// <param name="searchPattern">The search pattern to match against directory names.</param>
    /// <returns>An enumerable collection of matching directories.</returns>
    public IEnumerable<VirtualDirectory> EnumerateDirectories(string searchPattern) => EnumerateDirectories(searchPattern, SearchOption.TopDirectoryOnly);

    /// <summary>
    /// Returns the directories within this directory that match a search pattern.
    /// </summary>
    /// <param name="searchPattern">The search pattern to match against directory names.</param>
    /// <param name="searchOption">Whether to search only this directory or all sub-directories.</param>
    /// <returns>An enumerable collection of matching directories.</returns>
    public IEnumerable<VirtualDirectory> EnumerateDirectories(string searchPattern, SearchOption searchOption)
    {
        var pending = new Stack<VirtualDirectory>([this]);

        while (pending.TryPop(out var directory))
        {
            foreach (var child in directory.Directories)
            {
                if (FileSystemName.MatchesWin32Expression(searchPattern, child.Name))
                    yield return child;
                if (searchOption == SearchOption.AllDirectories)
                    pending.Push(child);
            }
        }
    }

    /// <summary>
    /// Returns the files directly within this directory.
    /// </summary>
    /// <returns>An enumerable collection of files.</returns>
    public IEnumerable<VirtualFile> EnumerateFiles() => EnumerateFiles("*", SearchOption.TopDirectoryOnly);

    /// <summary>
    /// Returns the files directly within this directory that match a search pattern.
    /// </summary>
    /// <param name="searchPattern">The search pattern to match against file names.</param>
    /// <returns>An enumerable collection of matching files.</returns>
    public IEnumerable<VirtualFile> EnumerateFiles(string searchPattern) => EnumerateFiles(searchPattern, SearchOption.TopDirectoryOnly);

    /// <summary>
    /// Returns the files within this directory that match a search pattern.
    /// </summary>
    /// <param name="searchPattern">The search pattern to match against file names.</param>
    /// <param name="searchOption">Whether to search only this directory or all sub-directories.</param>
    /// <returns>An enumerable collection of matching files.</returns>
    public IEnumerable<VirtualFile> EnumerateFiles(string searchPattern, SearchOption searchOption)
    {
        var pending = new Stack<VirtualDirectory>([this]);

        while (pending.TryPop(out var directory))
        {
            foreach (var file in directory.Files)
            {
                if (FileSystemName.MatchesWin32Expression(searchPattern, file.Name))
                    yield return file;
            }

            if (searchOption == SearchOption.AllDirectories)
            {
                foreach (var child in directory.Directories)
                    pending.Push(child);
            }
        }
    }

    /// <summary>
    /// Gets a file by its path relative to this directory.
    /// </summary>
    /// <param name="path">The path relative to this directory.</param>
    /// <returns>The matching file.</returns>
    /// <exception cref="FileNotFoundException">The path does not identify a file.</exception>
    public VirtualFile GetFile(string path) => TryGetFile(path, out var file) ? file : throw new FileNotFoundException("Could not find file.", path);

    /// <summary>
    /// Attempts to get a file by its path relative to this directory.
    /// </summary>
    /// <param name="path">The path relative to this directory.</param>
    /// <param name="file">Receives the matching file when found.</param>
    /// <returns><see langword="true"/> if the file exists; otherwise, <see langword="false"/>.</returns>
    public bool TryGetFile(string path, [NotNullWhen(true)] out VirtualFile? file)
    {
        file = null;

        var separator = path.AsSpan().LastIndexOfAny('/', '\\');
        var directory = this;

        if (separator >= 0 && !TryGetDirectory(path[..separator], out directory))
            return false;

        lock (_syncRoot)
            return directory._files.TryGetValue(path[(separator + 1)..], out file);
    }

    /// <summary>
    /// Attempts to get a file of the requested type by its path relative to this directory.
    /// </summary>
    /// <typeparam name="T">The file type to find.</typeparam>
    /// <param name="path">The path relative to this directory.</param>
    /// <param name="file">Receives the matching file when found.</param>
    /// <returns><see langword="true"/> if a matching file exists; otherwise, <see langword="false"/>.</returns>
    public bool TryGetFile<T>(string path, [NotNullWhen(true)] out T? file) where T : VirtualFile
    {
        file = TryGetFile(path, out var found) ? found as T : null;
        return file is not null;
    }

    /// <summary>
    /// Gets a directory by its path relative to this directory.
    /// </summary>
    /// <param name="path">The path relative to this directory.</param>
    /// <returns>The matching directory.</returns>
    /// <exception cref="DirectoryNotFoundException">The path does not identify a directory.</exception>
    public VirtualDirectory GetDirectory(string path) => TryGetDirectory(path, out var directory) ? directory : throw new DirectoryNotFoundException($"Could not find directory: {path}");

    /// <summary>
    /// Attempts to get a directory by its path relative to this directory.
    /// </summary>
    /// <param name="path">The path relative to this directory. An empty path returns this directory.</param>
    /// <param name="directory">Receives the matching directory when found.</param>
    /// <returns><see langword="true"/> if the directory exists; otherwise, <see langword="false"/>.</returns>
    public bool TryGetDirectory(string path, [NotNullWhen(true)] out VirtualDirectory? directory)
    {
        directory = this;

        lock (_syncRoot)
        {
            foreach (var name in path.Split(VirtualFileSystem.Separators, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!directory._directories.TryGetValue(name, out directory))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Extracts this directory and its contents to disk. Existing files are not overwritten.
    /// </summary>
    /// <param name="destinationDirectoryName">The directory to extract into.</param>
    public void ExtractToDirectory(string destinationDirectoryName) => ExtractToDirectory(destinationDirectoryName, false);

    /// <summary>
    /// Extracts this directory and its contents to disk. Characters that are invalid in file names on this platform are replaced with '_'.
    /// </summary>
    /// <param name="destinationDirectoryName">The directory to extract into.</param>
    /// <param name="overwriteFiles"><see langword="true"/> to overwrite existing files; otherwise, <see langword="false"/>.</param>
    public void ExtractToDirectory(string destinationDirectoryName, bool overwriteFiles)
    {
        Directory.CreateDirectory(destinationDirectoryName);

        foreach (var file in Files)
            file.ExtractToFile(Path.Join(destinationDirectoryName, GetSafeFileName(file.Name)), overwriteFiles);

        foreach (var directory in Directories)
            directory.ExtractToDirectory(Path.Join(destinationDirectoryName, GetSafeFileName(directory.Name)), overwriteFiles);
    }

    /// <inheritdoc/>
    public override string ToString() => Name;

    private static string GetSafeFileName(string name) => name.IndexOfAny(InvalidFileNameChars) < 0 ? name : string.Join('_', name.Split(InvalidFileNameChars));
}
