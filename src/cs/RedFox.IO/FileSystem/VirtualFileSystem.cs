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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.IO.FileSystem
{
    /// <summary>
    /// A class that provides a mechanism to store and manipulate a virtual file system.
    /// </summary>
    public class VirtualFileSystem
    {
        /// <summary>
        /// Invalid File Name Characters.
        /// </summary>
        internal static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

        /// <summary>
        /// Invalid Path Characters.
        /// </summary>
        internal static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();

        /// <summary>
        /// Gets the root directory.
        /// </summary>
        public VirtualDirectory Root { get; } = new();

        /// <summary>
        /// Returns an enumerable collection of the directories that meet specified criteria.
        /// </summary>
        /// <returns>An enumerable collection of the directories within the directory that match the specified search pattern and search option.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory could not be found.</exception>
        public IEnumerable<VirtualDirectory> EnumerateDirectories() => Root.EnumerateDirectories(null, "*", SearchOption.AllDirectories);

        /// <summary>
        /// Returns an enumerable collection of the directories that meet specified criteria.
        /// </summary>
        /// <param name="path">The path to the directory to search. This string is not case-sensitive. If null or empty, the search starts from the root directory.</param>
        /// <returns>An enumerable collection of the directories within the directory that match the specified search pattern and search option.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory could not be found.</exception>
        public IEnumerable<VirtualDirectory> EnumerateDirectories(string? path) => Root.EnumerateDirectories(path, "*", SearchOption.AllDirectories);

        /// <summary>
        /// Returns an enumerable collection of the directories that meet specified criteria.
        /// </summary>
        /// <param name="path">The path to the directory to search. This string is not case-sensitive. If null or empty, the search starts from the root directory.</param>
        /// <param name="searchPattern">The search string to match against the names.</param>
        /// <returns>An enumerable collection of the directories within the directory that match the specified search pattern and search option.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory could not be found.</exception>
        public IEnumerable<VirtualDirectory> EnumerateDirectories(string? path, string searchPattern) => Root.EnumerateDirectories(path, searchPattern, SearchOption.AllDirectories);

        /// <summary>
        /// Returns an enumerable collection of the directories that meet specified criteria.
        /// </summary>
        /// <param name="path">The path to the directory to search. This string is not case-sensitive. If null or empty, the search starts from the root directory.</param>
        /// <param name="searchPattern">The search string to match against the names.</param>
        /// <param name="searchOption">Specified the search option indicating whether to search all directories or only the top directory.</param>
        /// <returns>An enumerable collection of the directories within the directory that match the specified search pattern and search option.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory could not be found.</exception>
        public IEnumerable<VirtualDirectory> EnumerateDirectories(string? path, string searchPattern, SearchOption searchOption) => Root.EnumerateDirectories(path, searchPattern, searchOption);

        /// <summary>
        /// Returns an enumerable collection of the files that meet specified criteria.
        /// </summary>
        /// <returns>An enumerable collection of the files within the directory that match the specified search pattern and search option.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory could not be found.</exception>
        public IEnumerable<VirtualFile> EnumerateFiles() => Root.EnumerateFiles(null, "*", SearchOption.AllDirectories);

        /// <summary>
        /// Returns an enumerable collection of the files that meet specified criteria.
        /// </summary>
        /// <param name="path">The path to the directory to search. This string is not case-sensitive. If null or empty, the search starts from the root directory.</param>
        /// <returns>An enumerable collection of the files within the directory that match the specified search pattern and search option.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory could not be found.</exception>
        public IEnumerable<VirtualFile> EnumerateFiles(string? path) => Root.EnumerateFiles(path, "*", SearchOption.AllDirectories);

        /// <summary>
        /// Returns an enumerable collection of the files that meet specified criteria.
        /// </summary>
        /// <param name="path">The path to the directory to search. This string is not case-sensitive. If null or empty, the search starts from the root directory.</param>
        /// <param name="searchPattern">The search string to match against the names.</param>
        /// <returns>An enumerable collection of the files within the directory that match the specified search pattern and search option.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory could not be found.</exception>
        public IEnumerable<VirtualFile> EnumerateFiles(string? path, string searchPattern) => Root.EnumerateFiles(path, searchPattern, SearchOption.AllDirectories);

        /// <summary>
        /// Returns an enumerable collection of the files that meet specified criteria.
        /// </summary>
        /// <param name="path">The path to the directory to search. This string is not case-sensitive. If null or empty, the search starts from the root directory.</param>
        /// <param name="searchPattern">The search string to match against the names.</param>
        /// <param name="searchOption">Specified the search option indicating whether to search all directories or only the top directory.</param>
        /// <returns>An enumerable collection of the files within the directory that match the specified search pattern and search option.</returns>
        /// <exception cref="DirectoryNotFoundException">Thrown if the directory could not be found.</exception>
        public IEnumerable<VirtualFile> EnumerateFiles(string? path, string searchPattern, SearchOption searchOption) => Root.EnumerateFiles(path, searchPattern, searchOption);


        public void AddFile(string fullPath, VirtualFile file)
        {
            var directory = Root.CreateDirectories(Path.GetDirectoryName(fullPath)) ?? Root;
            file.MoveTo(directory);
        }

        public VirtualFile GetFile(string path) => Root.GetFile(path);

        public bool TryGetFile(string path, [NotNullWhen(true)] out VirtualFile? file) => Root.TryGetFile(path, out file);

        public static bool IsValidFileName(string name) =>
            !string.IsNullOrWhiteSpace(name) &&
            name.IndexOfAny(InvalidFileNameChars) == -1;

        public static bool IsValidDirectoryName(string name) =>
            !string.IsNullOrWhiteSpace(name) &&
            name.IndexOfAny(InvalidPathChars) == -1;

        public static bool IsValidFullPath(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return false;

            var parts = fullPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.All(IsValidDirectoryName) || parts.SkipLast(1).All(IsValidDirectoryName) && IsValidFileName(parts.Last());
        }
    }
}
