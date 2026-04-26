using RedFox.Graphics3D;
using System.Collections.ObjectModel;

namespace RedFox.Samples.Examples;

internal sealed class SceneNodeItem
{
    public SceneNodeItem(SceneNode node)
    {
        Node = node;
        foreach (SceneNode child in node.EnumerateChildren())
        {
            Children.Add(new SceneNodeItem(child));
        }
    }

    public SceneNode Node { get; }

    public ObservableCollection<SceneNodeItem> Children { get; } = [];

    public string Name => Node.Name;
}
