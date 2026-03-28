using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace RedFox.Graphics3D
{
    public class Scene : IUpdatable
    {
        public string Name { get; set; }

        public SceneRoot RootNode { get; internal set; }

        public Scene(string name)
        {
            Name = name;
            RootNode = new SceneRoot(this);
        }

        public Scene() : this("Untitled Scene")
        {

        }

        public void Update(float deltaTime) => RootNode.Update(deltaTime);

        public bool RemoveNode(SceneNode node) => RootNode.RemoveNode(node);

        public bool RemoveNode(string name) => RootNode.RemoveNode(name);

        public void ClearNodes() => RootNode.ClearNodes();

        public IEnumerable<SceneNode> EnumerateChildren() => RootNode.EnumerateChildren();

        public IEnumerable<T> EnumerateChildren<T>() where T : SceneNode => RootNode.EnumerateChildren<T>();

        public IEnumerable<SceneNode> EnumerateDescendants() => RootNode.EnumerateDescendants();

        public IEnumerable<T> EnumerateDescendants<T>() where T : SceneNode => RootNode.EnumerateDescendants<T>();

        public IEnumerable<SceneNode> EnumerateAncestors() => RootNode.EnumerateAncestors();

        public SceneNode[] GetDescendants() => RootNode.GetDescendants();

        public T[] GetDescendants<T>() where T : SceneNode => RootNode.GetDescendants<T>();

        public SceneNode[] GetAncestors() => RootNode.GetAncestors();

        public T[] GetAncestors<T>() where T : SceneNode => RootNode.GetAncestors<T>();

        public SceneNode FindByPath(string path) => RootNode.FindByPath(path);

        public bool TryFindByPath(string path, out SceneNode? node) => RootNode.TryFindByPath(path, out node);

        public IEnumerable<SceneNode> EnumerateSiblings() => RootNode.EnumerateSiblings();

        public SceneNode[] GetSiblings() => RootNode.GetSiblings();

        public void Traverse(Action<SceneNode> action) => RootNode.Traverse(action);

        public T FirstOfType<T>() where T : SceneNode => RootNode.FirstOfType<T>();

        public T? TryGetFirstOfType<T>() where T : SceneNode => RootNode.TryGetFirstOfType<T>();

        public bool TryGetFirstOfType<T>([NotNullWhen(true)] out T? node) where T : SceneNode => RootNode.TryGetFirstOfType<T>(out node);

        /// <inheritdoc/>
        public override string ToString() => Name;
    }
}
