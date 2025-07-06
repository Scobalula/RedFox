// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.IO.FileSystem
{
    /// <summary>
    /// A class to hold and manipulate a virtual directory that contains files and sub-directories.
    /// </summary>
    public class VirtualDirectory
    {
        /// <summary>
        /// Directories stored within this directory.
        /// </summary>
        internal readonly HashSet<VirtualDirectory> _directories =[];

        /// <summary>
        /// Files stored within this directory.
        /// </summary>
        internal readonly HashSet<VirtualFile> _files = [];

        /// <summary>
        /// Gets the parent directory.
        /// </summary>
        public VirtualDirectory? Parent { get; private set; }

        /// <summary>
        /// Gets or Sets the name of the directory.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the full path of the directory.
        /// </summary>
        public string FullPath => Parent == null ? Name : Path.Combine(Parent.FullPath, Name);

        /// <summary>
        /// Gets the directories stored within this directory.
        /// </summary>
        public IReadOnlyCollection<VirtualDirectory> Directories => _directories;

        /// <summary>
        /// Gets the files stored within this directory.
        /// </summary>
        public IReadOnlyCollection<VirtualFile> Files => _files;

        /// <remarks>
        /// Initializes a new instance of the <see cref="VirtualDirectory"/>.
        /// </remarks>
        internal VirtualDirectory()
        {
            Name = string.Empty;
        }

        /// <remarks>
        /// Initializes a new instance of the <see cref="VirtualDirectory"/> with the provided name.
        /// </remarks>
        /// <param name="name">The name of the directory.</param>
        public VirtualDirectory(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name), "Empty name provided.");
            if (name.IndexOfAny(VirtualFileSystem.InvalidFileNameChars) > -1)
                throw new ArgumentException("Name contains invalid characters.", nameof(name));

            Name = name;
        }

        /// <summary>
        /// Moves the this directory into the provided directory.
        /// </summary>
        /// <param name="newParent">The directory to move this directory into.</param>
        public void MoveTo(VirtualDirectory? newParent)
        {
            if (newParent == Parent)
                return;

            Parent?._directories.Remove(this);
            Parent = newParent;

            if (newParent is not null && !newParent._directories.Add(this))
            {
                throw new IOException($"Directory: {newParent.FullPath} already contains a directory with the name: {Name}");
            }
        }

        /// <summary>
        /// Create a new directory to this directory.
        /// </summary>
        /// <param name="name">The name of the directory.</param>
        /// <returns>The resulting directory.</returns>
        public VirtualDirectory CreateDirectory(string name)
        {
            var newDirectory = new VirtualDirectory(name);
            newDirectory.MoveTo(this);
            return newDirectory;
        }

        /// <summary>
        /// Creates a new directory and directories within this directory.
        /// </summary>
        /// <param name="fullPath">The full directory path.</param>
        /// <returns>The resulting directory at the end of the path.</returns>
        public VirtualDirectory? CreateDirectories(string? fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return null;

            var parts = fullPath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);

            var current = this;

            for (int i = 0; i < parts.Length; i++)
            {
                current = current._directories.FirstOrDefault(x => x.Name.Equals(parts[i], StringComparison.OrdinalIgnoreCase)) ?? current.CreateDirectory(parts[i]);
            }

            return current;
        }

        public bool TryGetDirectory(string fullPath, [NotNullWhen(true)] out VirtualDirectory? directory)
        {
            directory = null;

            if (string.IsNullOrWhiteSpace(fullPath))
                return false;

            string[] parts = fullPath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);

            var current = this;

            for (int i = 0; i < parts.Length; i++)
            {
                string dirName = parts[i];

                var dir = current._directories.FirstOrDefault(x => x.Name.Equals(dirName, StringComparison.OrdinalIgnoreCase));

                if (dir is null)
                {
                    return false;
                }

                current = dir;
            }

            directory = current;
            return true;
        }

        /// <summary>
        /// Returns an enumerable collection of the directories that meet specified criteria.
        /// </summary>
        /// <returns>An enumerable collection of the directories within the directory that match the specified search pattern and search option.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory could not be found.</exception>
        public IEnumerable<VirtualDirectory> EnumerateDirectories() => EnumerateDirectories(null, "*", SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Returns an enumerable collection of the directories that meet specified criteria.
        /// </summary>
        /// <param name="path">The path to the directory to search. This string is not case-sensitive. If null or empty, the search starts from this directory.</param>
        /// <returns>An enumerable collection of the directories within the directory that match the specified search pattern and search option.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory could not be found.</exception>
        public IEnumerable<VirtualDirectory> EnumerateDirectories(string? path) => EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Returns an enumerable collection of the directories that meet specified criteria.
        /// </summary>
        /// <param name="path">The path to the directory to search. This string is not case-sensitive. If null or empty, the search starts from this directory.</param>
        /// <param name="searchPattern">The search string to match against the names.</param>
        /// <returns>An enumerable collection of the directories within the directory that match the specified search pattern and search option.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory could not be found.</exception>
        public IEnumerable<VirtualDirectory> EnumerateDirectories(string? path, string searchPattern) => EnumerateDirectories(path, searchPattern, SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Returns an enumerable collection of the directories that meet specified criteria.
        /// </summary>
        /// <param name="path">The path to the directory to search. This string is not case-sensitive. If null or empty, the search starts from this directory.</param>
        /// <param name="searchPattern">The search string to match against the names.</param>
        /// <param name="searchOption">Specified the search option indicating whether to search all directories or only the top directory.</param>
        /// <returns>An enumerable collection of the directories within the directory that match the specified search pattern and search option.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory could not be found.</exception>
        public IEnumerable<VirtualDirectory> EnumerateDirectories(string? path, string searchPattern, SearchOption searchOption)
        {
            var directory = this;

            if (!string.IsNullOrWhiteSpace(path))
            {
                if (!TryGetDirectory(path, out directory))
                {
                    throw new DirectoryNotFoundException($"Failed to find directory: {path}");
                }
            }

            foreach (var dir in directory._directories)
            {
                if (FileSystemName.MatchesWin32Expression(searchPattern, dir.Name))
                    yield return dir;

                if (searchOption == SearchOption.AllDirectories)
                {
                    foreach (var subDir in dir.EnumerateDirectories(null, searchPattern, searchOption))
                    {
                        if (FileSystemName.MatchesWin32Expression(searchPattern, subDir.Name))
                            yield return subDir;
                    }
                }
            }
        }

        /// <summary>
        /// Returns an enumerable collection of the files that meet specified criteria.
        /// </summary>
        /// <returns>An enumerable collection of the files within the directory that match the specified search pattern and search option.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory could not be found.</exception>
        public IEnumerable<VirtualFile> EnumerateFiles() => EnumerateFiles(null, "*", SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Returns an enumerable collection of the files that meet specified criteria.
        /// </summary>
        /// <param name="path">The path to the directory to search. This string is not case-sensitive. If null or empty, the search starts from this directory.</param>
        /// <returns>An enumerable collection of the files within the directory that match the specified search pattern and search option.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory could not be found.</exception>
        public IEnumerable<VirtualFile> EnumerateFiles(string? path) => EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Returns an enumerable collection of the files that meet specified criteria.
        /// </summary>
        /// <param name="path">The path to the directory to search. This string is not case-sensitive. If null or empty, the search starts from this directory.</param>
        /// <param name="searchPattern">The search string to match against the names.</param>
        /// <returns>An enumerable collection of the files within the directory that match the specified search pattern and search option.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory could not be found.</exception>
        public IEnumerable<VirtualFile> EnumerateFiles(string? path, string searchPattern) => EnumerateFiles(path, searchPattern, SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Returns an enumerable collection of the files that meet specified criteria.
        /// </summary>
        /// <param name="path">The path to the directory to search. This string is not case-sensitive. If null or empty, the search starts from this directory.</param>
        /// <param name="searchPattern">The search string to match against the names.</param>
        /// <param name="searchOption">Specified the search option indicating whether to search all directories or only the top directory.</param>
        /// <returns>An enumerable collection of the files within the directory that match the specified search pattern and search option.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory could not be found.</exception>
        public IEnumerable<VirtualFile> EnumerateFiles(string? path, string searchPattern, SearchOption searchOption)
        {
            var directory = this;

            if (!string.IsNullOrWhiteSpace(path))
            {
                if (!TryGetDirectory(path, out directory))
                {
                    throw new DirectoryNotFoundException($"Failed to find directory: {path}");
                }
            }

            foreach (var file in directory._files)
            {
                if (FileSystemName.MatchesWin32Expression(searchPattern, file.Name))
                    yield return file;
            }

            if (searchOption == SearchOption.AllDirectories)
            {
                foreach (var dir in EnumerateDirectories(null, "*", searchOption))
                {
                    foreach (var file in dir._files)
                    {
                        if (FileSystemName.MatchesWin32Expression(searchPattern, file.Name))
                            yield return file;
                    }
                }
            }
        }

        public VirtualFile GetFile(string path)
        {
            if (!TryGetFile(path, out var file))
                throw new FileNotFoundException("Could not find file.", path);

            return file;
        }

        public bool TryGetFile(string path, [NotNullWhen(true)] out VirtualFile? file)
        {
            file = null;

            if (string.IsNullOrWhiteSpace(path))
                return false;

            string[] parts = path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);

            var current = this;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                string dirName = parts[i];

                var dir = current._directories.FirstOrDefault(x => x.Name.Equals(dirName, StringComparison.OrdinalIgnoreCase));

                if (dir is null)
                {
                    return false;
                }

                current = dir;
            }

            file = current._files.FirstOrDefault(x => x.Name == parts[^1]);
            return file is not null;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Name.ToLower().GetHashCode();
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (obj is VirtualDirectory directory)
                return directory.Name.Equals(Name, StringComparison.OrdinalIgnoreCase);

            return base.Equals(obj);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Name;
        }
    }
}
