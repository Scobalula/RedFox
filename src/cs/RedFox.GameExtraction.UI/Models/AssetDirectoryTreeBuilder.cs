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

        MutableNode root = new(string.Empty, string.Empty, parent: null);

        foreach (AssetRowViewModel row in rows)
        {
            string assetName = row.Asset.Name ?? string.Empty;
            string[] segments = assetName.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length <= 1)
            {
                root.Files.Add(row);
                continue;
            }

            MutableNode current = root;
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
    public static AssetDirectoryNode BuildFromVirtualFileSystem(
        VirtualFileSystem fileSystem,
        IReadOnlyList<AssetRowViewModel> rows)
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

        MutableNode root = new(string.Empty, string.Empty, parent: null);
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

            MutableNode current = root;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                current = current.GetOrAddChild(segments[i]);
            }

            current.Files.Add(row);
        }

        return root.Freeze();
    }

    private static void PopulateFromVirtualDirectory(
        MutableNode node,
        VirtualDirectory directory,
        Dictionary<VirtualFile, AssetRowViewModel> rowsByFile)
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
            MutableNode childNode = node.GetOrAddChild(child.Name);
            PopulateFromVirtualDirectory(childNode, child, rowsByFile);
        }
    }

    private sealed class MutableNode
    {
        private readonly Dictionary<string, MutableNode> _childIndex = new(StringComparer.OrdinalIgnoreCase);

        public MutableNode(string name, string fullPath, MutableNode? parent)
        {
            Name = name;
            FullPath = fullPath;
            Parent = parent;
        }

        public string Name { get; }

        public string FullPath { get; }

        public MutableNode? Parent { get; }

        public List<MutableNode> Children { get; } = [];

        public List<AssetRowViewModel> Files { get; } = [];

        public MutableNode GetOrAddChild(string name)
        {
            if (_childIndex.TryGetValue(name, out MutableNode? existing))
            {
                return existing;
            }

            string childPath = string.IsNullOrEmpty(FullPath) ? name : $"{FullPath}/{name}";
            MutableNode child = new(name, childPath, this);
            _childIndex.Add(name, child);
            Children.Add(child);
            return child;
        }

        public AssetDirectoryNode Freeze()
        {
            AssetDirectoryNode node = FreezeRecursive();
            SetParents(node, parent: null);
            return node;
        }

        private static void SetParents(AssetDirectoryNode node, AssetDirectoryNode? parent)
        {
            node.Parent = parent;
            for (int i = 0; i < node.Children.Count; i++)
            {
                SetParents(node.Children[i], node);
            }
        }

        private AssetDirectoryNode FreezeRecursive()
        {
            Children.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            Files.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            AssetDirectoryNode[] frozenChildren = new AssetDirectoryNode[Children.Count];
            int recursiveCount = Files.Count;
            for (int i = 0; i < Children.Count; i++)
            {
                AssetDirectoryNode frozenChild = Children[i].FreezeRecursive();
                frozenChildren[i] = frozenChild;
                recursiveCount += frozenChild.RecursiveFileCount;
            }

            return new AssetDirectoryNode(
                Name,
                FullPath,
                parent: null,
                frozenChildren,
                Files,
                recursiveCount);
        }
    }
}
