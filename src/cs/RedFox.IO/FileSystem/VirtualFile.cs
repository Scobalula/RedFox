// --------------------------------------------------------------------------------------
// RedFox Utility Library
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------
namespace RedFox.IO.FileSystem;

/// <summary>
/// An abstract class that defines a file stored within a <see cref="VirtualFileSystem"/>.
/// </summary>
/// <param name="size">The size of the file in bytes.</param>
public abstract class VirtualFile(long size)
{
    internal VirtualFile? Shadowed { get; set; }

    /// <summary>
    /// Gets the parent directory, or <see langword="null"/> if the file has not been added to a file system.
    /// </summary>
    public VirtualDirectory? Parent { get; internal set; }

    /// <summary>
    /// Gets the name of the file, assigned when the file is added to a file system.
    /// </summary>
    public string Name { get; internal set; } = string.Empty;

    /// <summary>
    /// Gets the extension of the file.
    /// </summary>
    public string Extension => Path.GetExtension(Name);

    /// <summary>
    /// Gets the full path of the file, using '/' as the separator.
    /// </summary>
    public string FullPath => Parent is null || Parent.Parent is null ? Name : $"{Parent.FullPath}/{Name}";

    /// <summary>
    /// Gets whether another file added later at the same path is hiding this file.
    /// </summary>
    public bool IsShadowed
    {
        get
        {
            if (Parent is not { } parent)
                return false;

            lock (parent._syncRoot)
                return parent._files.TryGetValue(Name, out var visible) && visible != this;
        }
    }

    /// <summary>
    /// Gets the size of the file in bytes.
    /// </summary>
    public long Size { get; } = size;

    /// <summary>
    /// Gets or sets custom data associated with the file.
    /// </summary>
    public object? Tag { get; set; }

    /// <summary>
    /// Opens the file for reading.
    /// </summary>
    /// <returns>A stream containing the file contents.</returns>
    public abstract Stream Open();

    /// <summary>
    /// Reads the contents of the file into a byte array.
    /// </summary>
    /// <returns>A byte array containing the file contents.</returns>
    public virtual byte[] ReadAllBytes()
    {
        using var stream = Open();

        var buffer = new byte[Size];
        stream.ReadExactly(buffer);

        return buffer;
    }

    /// <summary>
    /// Extracts the file to disk. An existing file is not overwritten.
    /// </summary>
    /// <param name="destinationFileName">The path of the file to create.</param>
    public void ExtractToFile(string destinationFileName) => ExtractToFile(destinationFileName, false);

    /// <summary>
    /// Extracts the file to disk.
    /// </summary>
    /// <param name="destinationFileName">The path of the file to create.</param>
    /// <param name="overwrite"><see langword="true"/> to overwrite an existing file; otherwise, <see langword="false"/>.</param>
    public virtual void ExtractToFile(string destinationFileName, bool overwrite)
    {
        using var stream = Open();
        using var output = new FileStream(destinationFileName, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write);

        stream.CopyTo(output);
    }

    /// <inheritdoc/>
    public override string ToString() => Name;
}
