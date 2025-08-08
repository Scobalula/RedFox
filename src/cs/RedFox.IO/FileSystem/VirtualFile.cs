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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.IO.FileSystem
{
    /// <summary>
    /// An abstract class that defines methods and properties to manilpulate a virtual file.
    /// </summary>
    public abstract class VirtualFile
    {
        /// <summary>
        /// Gets the parent directory.
        /// </summary>
        public VirtualDirectory? Parent { get; internal set; }

        /// <summary>
        /// Gets or Sets the name of the file.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the extension of the file.
        /// </summary>
        public string Extension => Path.GetExtension(Name);

        /// <summary>
        /// Gets the full path of the file.
        /// </summary>
        public string FullPath => Parent == null ? Name : Path.Combine(Parent.FullPath, Name);

        /// <summary>
        /// Gets the full directory path where the file is stored.
        /// </summary>
        public string? DirectoryPath => Parent?.FullPath;

        /// <summary>
        /// Gets the size of the file.
        /// </summary>
        public long Size { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualFile"/> class with the given name and size.
        /// </summary>
        /// <param name="name">The name of the file.</param>
        /// <param name="size">The size of the file in bytes.</param>
        protected VirtualFile(string name, long size)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name), "Empty name provided.");
            if (name.IndexOfAny(VirtualFileSystem.InvalidFileNameChars) > -1)
                throw new ArgumentException("Name contains invalid characters.", nameof(name));

            Name = name;
            Size = size;
        }

        /// <summary>
        /// Moves the this file into the provided directory.
        /// </summary>
        /// <param name="newParent">The directory to move this file into.</param>
        public void MoveTo(VirtualDirectory? newParent)
        {
            if (newParent == Parent)
                return;

            Parent?._files.Remove(this);
            Parent = newParent;

            if (newParent is not null && !newParent._files.Add(this))
            {
                throw new IOException($"Directory: {newParent.FullPath} already contains a file with the name: {Name}");
            }
        }

        /// <summary>
        /// Opens the file as a <see cref="Stream"/>.
        /// </summary>
        /// <returns>Resulting stream of bytes.</returns>
        public abstract Stream Open();

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Name.ToLower().GetHashCode();
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (obj is VirtualFile directory)
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
