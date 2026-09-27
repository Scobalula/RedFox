namespace RedFox.GameExtraction.UI.Models;

/// <summary>
/// Represents a folder in the asset directory tree presented in directory view mode.
/// </summary>
/// <param name="name">The folder display name. Empty for the synthetic root.</param>
/// <param name="fullPath">The forward-slash separated path from the root. Empty for the root.</param>
/// <param name="parent">The parent node, or <see langword="null"/> for the root.</param>
/// <param name="children">The child folder nodes, already sorted.</param>
/// <param name="files">The files contained directly in this folder, already sorted.</param>
/// <param name="recursiveFileCount">The total number of files contained in this folder and all descendants.</param>
public sealed class AssetDirectoryNode(string name, string fullPath, AssetDirectoryNode? parent, IReadOnlyList<AssetDirectoryNode> children, IReadOnlyList<AssetRowViewModel> files, int recursiveFileCount)
{
    /// <summary>
    /// Gets the folder display name. Empty for the synthetic root.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the forward-slash separated path from the root. Empty for the root.
    /// </summary>
    public string FullPath { get; } = fullPath;

    /// <summary>
    /// Gets the parent folder, or <c>null</c> for the root.
    /// </summary>
    public AssetDirectoryNode? Parent { get; internal set; } = parent;

    /// <summary>
    /// Gets the child folder nodes.
    /// </summary>
    public IReadOnlyList<AssetDirectoryNode> Children { get; } = children;

    /// <summary>
    /// Gets the asset rows contained directly in this folder.
    /// </summary>
    public IReadOnlyList<AssetRowViewModel> Files { get; } = files;

    /// <summary>
    /// Gets the total number of files contained in this folder and all descendants.
    /// </summary>
    public int RecursiveFileCount { get; } = recursiveFileCount;

    /// <summary>
    /// Gets the display label combining the folder name and recursive file count.
    /// </summary>
    public string DisplayLabel => string.IsNullOrEmpty(Name)
        ? $"(root) ({RecursiveFileCount:N0})"
        : $"{Name} ({RecursiveFileCount:N0})";
}
