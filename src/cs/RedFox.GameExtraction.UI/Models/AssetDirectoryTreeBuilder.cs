using RedFox.IO.FileSystem;

namespace RedFox.GameExtraction.UI.Models;

/// <summary>
/// Builds an <see cref="AssetDirectoryNode"/> tree from a flat asset row list or a
/// pre-populated <see cref="VirtualFileSystem"/>.
/// </summary>
public static class AssetDirectoryTreeBuilder
{
    private static readonly char[] PathSeparators = ['/', '\\'];

    /// <summary>
    /// Builds a directory tree by splitting <see cref="AssetRowViewModel.Asset"/>'s
    /// <see cref="Asset.Name"/> on directory separators.
    /// </summary>
    /// <param name="rows">The asset rows to organize.</param>
    /// <returns>The synthetic root node. Folders without a path land directly under it.</returns>
    public static AssetDirectoryNode BuildFromAssetNames(IReadOnlyList<AssetRowViewModel> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        MutableAssetDirectoryNode root = new(string.Empty, string.Empty);

        foreach (AssetRowViewModel row in rows)
        {
            string assetName = row.Asset.Name ?? string.Empty;
            string[] segments = assetName.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length <= 1)
            {
                root.Files.Add(row);
                continue;
            }

            MutableAssetDirectoryNode current = root;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                current = current.GetOrAddChild(segments[i]);
            }

            current.Files.Add(row);
        }

        return root.Freeze();
    }

    /// <summary>
    /// Builds a directory tree from a populated <see cref="VirtualFileSystem"/>. Asset
    /// rows whose <see cref="Asset.DataSource"/> is a <see cref="VirtualFile"/> are
    /// placed at the file's location in the virtual hierarchy; any rows without a
    /// matching virtual file fall back to splitting their <see cref="Asset.Name"/>.
    /// </summary>
    /// <param name="fileSystem">The virtual file system to mirror.</param>
    /// <param name="rows">The asset rows being displayed.</param>
    public static AssetDirectoryNode BuildFromVirtualFileSystem(VirtualFileSystem fileSystem, IReadOnlyList<AssetRowViewModel> rows)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(rows);

        Dictionary<VirtualFile, AssetRowViewModel> rowsByFile = new(ReferenceEqualityComparer.Instance);
        List<AssetRowViewModel> unmapped = [];
        foreach (AssetRowViewModel row in rows)
        {
            if (row.Asset.DataSource is VirtualFile file)
            {
                rowsByFile[file] = row;
            }
            else
            {
                unmapped.Add(row);
            }
        }

        MutableAssetDirectoryNode root = new(string.Empty, string.Empty);
        PopulateFromVirtualDirectory(root, fileSystem.Root, rowsByFile);

        foreach (AssetRowViewModel row in unmapped)
        {
            string assetName = row.Asset.Name ?? string.Empty;
            string[] segments = assetName.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length <= 1)
            {
                root.Files.Add(row);
                continue;
            }

            MutableAssetDirectoryNode current = root;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                current = current.GetOrAddChild(segments[i]);
            }

            current.Files.Add(row);
        }

        return root.Freeze();
    }

    private static void PopulateFromVirtualDirectory(MutableAssetDirectoryNode node, VirtualDirectory directory, Dictionary<VirtualFile, AssetRowViewModel> rowsByFile)
    {
        foreach (VirtualFile file in directory.Files)
        {
            if (rowsByFile.TryGetValue(file, out AssetRowViewModel? row))
            {
                node.Files.Add(row);
            }
        }

        foreach (VirtualDirectory child in directory.Directories)
        {
            MutableAssetDirectoryNode childNode = node.GetOrAddChild(child.Name);
            PopulateFromVirtualDirectory(childNode, child, rowsByFile);
        }
    }

}
