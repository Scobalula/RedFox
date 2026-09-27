namespace RedFox.GameExtraction.UI.Models;

internal sealed class MutableAssetDirectoryNode(string name, string fullPath)
{
    private readonly Dictionary<string, MutableAssetDirectoryNode> _childIndex = new(StringComparer.OrdinalIgnoreCase);

    public string Name { get; } = name;

    public string FullPath { get; } = fullPath;

    public List<MutableAssetDirectoryNode> Children { get; } = [];

    public List<AssetRowViewModel> Files { get; } = [];

    public MutableAssetDirectoryNode GetOrAddChild(string childName)
    {
        if (_childIndex.TryGetValue(childName, out MutableAssetDirectoryNode? existing))
            return existing;

        string childPath = string.IsNullOrEmpty(FullPath) ? childName : $"{FullPath}/{childName}";
        MutableAssetDirectoryNode child = new(childName, childPath);
        _childIndex.Add(childName, child);
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
            SetParents(node.Children[i], node);
    }

    private AssetDirectoryNode FreezeRecursive()
    {
        Children.Sort(static (left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
        Files.Sort(static (left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));

        AssetDirectoryNode[] frozenChildren = new AssetDirectoryNode[Children.Count];
        int recursiveCount = Files.Count;
        for (int i = 0; i < Children.Count; i++)
        {
            AssetDirectoryNode frozenChild = Children[i].FreezeRecursive();
            frozenChildren[i] = frozenChild;
            recursiveCount += frozenChild.RecursiveFileCount;
        }

        return new AssetDirectoryNode(Name, FullPath, parent: null, frozenChildren, Files, recursiveCount);
    }
}
