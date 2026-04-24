using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Xml.Linq;
using RedFox.Graphics3D.Rendering.Backend;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Represents a node in a scene graph. Scene nodes can have a parent and zero or more
    /// child nodes, carry transform information and optional rendering handles, and
    /// participate in update traversal.
    /// </summary>
    public abstract class SceneNode : IUpdatable, IDisposable
    {
        private List<SceneNode>? _children = null;
        private bool _disposed;
        private Scene? _scene = null;

        /// <summary>
        /// Gets or sets the name associated with the node.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the scene this this object is apart of.
        /// </summary>
        public Scene? Scene => _scene;

        /// <summary>
        /// Gets or Sets the parent of this node.
        /// </summary>
        public SceneNode? Parent { get; internal set; }

        /// <summary>
        /// Gets the collection of child scene nodes associated with this node.
        /// </summary>
        public IReadOnlyCollection<SceneNode>? Children => _children;

        /// <summary>
        /// Gets or sets the bind transformation applied to the node.
        /// For bones, this represents the skin binding pose and default fallback pose.
        /// </summary>
        public Transform BindTransform { get; set; } = new();

        /// <summary>
        /// Gets or sets the live transformation applied to the node.
        /// </summary>
        public Transform LiveTransform { get; set; } = new();

        /// <summary>
        /// Gets or sets the renderer-specific handle used for rendering operations.
        /// </summary>
        public IRenderHandle? GraphicsHandle { get; set; }

        /// <summary>
        /// Gets or sets the flags that define the properties and behaviors of the scene node.
        /// </summary>
        public SceneNodeFlags Flags { get; set; }

        /// <summary>
        /// Initializes a new instance of <see cref="SceneNode"/> with a generated name.
        /// </summary>
        public SceneNode() : this($"SceneNode{SceneNodeId.GetNextId()}")
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="SceneNode"/> with a generated name and
        /// the specified flags.
        /// </summary>
        /// <param name="flags">The flags that control node behavior.</param>
        public SceneNode(SceneNodeFlags flags) : this($"SceneNode{SceneNodeId.GetNextId()}")
        {
            Flags = flags;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="SceneNode"/> with the specified name.
        /// </summary>
        /// <param name="name">The name to assign to the node.</param>
        public SceneNode(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="SceneNode"/> with the specified name
        /// and flags.
        /// </summary>
        /// <param name="name">The name to assign to the node.</param>
        /// <param name="flags">The flags that control node behavior.</param>
        public SceneNode(string name, SceneNodeFlags flags)
        {
            Name = name;
            Flags = flags;
        }

        /// <summary>
        /// Creates the render handle for this node.
        /// </summary>
        /// <param name="graphicsDevice">The graphics device that will own the handle resources.</param>
        /// <param name="materialTypes">The material type registry used to resolve material pipelines.</param>
        /// <returns>The created render handle, or <see langword="null"/> when this node does not render.</returns>
        public virtual IRenderHandle? CreateRenderHandle(IGraphicsDevice graphicsDevice, IMaterialTypeRegistry materialTypes)
        {
            ArgumentNullException.ThrowIfNull(graphicsDevice);
            ArgumentNullException.ThrowIfNull(materialTypes);
            return null;
        }

        private bool MatchesFilter(SceneNodeFlags filter) =>
            filter == SceneNodeFlags.None || (Flags & filter) == filter;

        /// <summary>
        /// Moves this node to a new parent while preserving world transforms.
        /// </summary>
        /// <param name="newParent">The new parent node, or <see langword="null"/> to detach.</param>
        /// <exception cref="SceneNodeDuplicateException">Thrown if a node with the same name already exists in the new parent.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an invalid transform mode is specified.</exception>
        public void MoveTo(SceneNode? newParent) => MoveTo(newParent, ReparentTransformMode.PreserveWorld);

        /// <summary>
        /// Moves this node to a new parent while preserving the selected transform space.
        /// </summary>
        /// <param name="newParent">The new parent node, or <see langword="null"/> to detach.</param>
        /// <param name="transformMode">The transform preservation mode used during reparenting.</param>
        /// <exception cref="SceneNodeDuplicateException">Thrown if a node with the same name already exists in the new parent.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an invalid transform mode is specified.</exception>
        public void MoveTo(SceneNode? newParent, ReparentTransformMode transformMode)
        {
            if (newParent == Parent)
                return;

            Parent?._children?.Remove(this);

            if (newParent is not null)
            {
                Parent = newParent;

                if (newParent?._children is not null && newParent._children.FindIndex(x => x.Name.Equals(Name, StringComparison.CurrentCultureIgnoreCase)) != -1)
                {
                    throw new SceneNodeDuplicateException($"A node with the name: {Name} already exists in: {newParent.Name}");
                }

                Parent._children ??= [];
                Parent._children.Add(this);
            }

            switch (transformMode)
            {
                case ReparentTransformMode.PreserveLocal:
                    BindTransform.WorldPosition = null;
                    BindTransform.WorldRotation = null;
                    LiveTransform.WorldPosition = null;
                    LiveTransform.WorldRotation = null;
                    break;

                case ReparentTransformMode.PreserveWorld:
                    BindTransform.LocalPosition = null;
                    BindTransform.LocalRotation = null;
                    LiveTransform.LocalPosition = null;
                    LiveTransform.LocalRotation = null;
                    break;

                case ReparentTransformMode.PreserveExisting:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(transformMode), transformMode, "Unknown reparent transform mode.");
            }

            foreach (var node in EnumerateDescendants())
            {
                node.BindTransform.WorldPosition = null;
                node.BindTransform.WorldRotation = null;
                node.LiveTransform.WorldPosition = null;
                node.LiveTransform.WorldRotation = null;
            }
        }

        /// <summary>
        /// Determines whether this node is a descendant of the specified node.
        /// </summary>
        /// <param name="node">The potential ancestor node.</param>
        /// <returns><see langword="true"/> if this node is a descendant; otherwise <see langword="false"/>.</returns>
        public bool IsDescendantOf(SceneNode node) => IsDescendantOf(node, SceneNodeFlags.None);

        /// <summary>
        /// Determines whether this node is a descendant of the specified node that matches the given filter.
        /// </summary>
        /// <param name="node">The potential ancestor node.</param>
        /// <param name="filter">The flags the matching ancestor must contain.</param>
        /// <returns><see langword="true"/> if a matching ancestor is found; otherwise <see langword="false"/>.</returns>
        public bool IsDescendantOf(SceneNode node, SceneNodeFlags filter)
        {
            var current = Parent;

            while (current != null)
            {
                if (current == node)
                    return current.MatchesFilter(filter);

                current = current.Parent;
            }

            return false;
        }

        /// <summary>
        /// Determines whether this node is a descendant of an ancestor with the specified name
        /// using the current culture for comparison.
        /// </summary>
        /// <param name="name">The name of the ancestor to test for.</param>
        /// <returns><see langword="true"/> if an ancestor with the given name exists; otherwise <see langword="false"/>.</returns>
        public bool IsDescendantOf(string name) => IsDescendantOf(name, StringComparison.CurrentCulture, SceneNodeFlags.None);

        /// <summary>
        /// Determines whether this node is a descendant of an ancestor with the specified name
        /// using the current culture for comparison and the provided filter.
        /// </summary>
        /// <param name="name">The name of the ancestor to test for.</param>
        /// <param name="filter">The flags the matching ancestor must contain.</param>
        /// <returns><see langword="true"/> if an ancestor with the given name exists; otherwise <see langword="false"/>.</returns>
        public bool IsDescendantOf(string name, SceneNodeFlags filter) =>
            IsDescendantOf(name, StringComparison.CurrentCulture, filter);

        /// <summary>
        /// Determines whether this node is a descendant of an ancestor with the specified name
        /// using the provided string comparison.
        /// </summary>
        /// <param name="name">The name of the ancestor to test for.</param>
        /// <param name="comparisonType">The string comparison to use when comparing names.</param>
        /// <returns><see langword="true"/> if an ancestor with the given name exists; otherwise <see langword="false"/>.</returns>
        public bool IsDescendantOf(string name, StringComparison comparisonType) =>
            IsDescendantOf(name, comparisonType, SceneNodeFlags.None);

        /// <summary>
        /// Determines whether this node is a descendant of an ancestor with the specified name
        /// using the provided string comparison and filter.
        /// </summary>
        /// <param name="name">The name of the ancestor to test for.</param>
        /// <param name="comparisonType">The string comparison to use when comparing names.</param>
        /// <param name="filter">The flags the matching ancestor must contain.</param>
        /// <returns><see langword="true"/> if an ancestor with the given name exists; otherwise <see langword="false"/>.</returns>
        public bool IsDescendantOf(string name, StringComparison comparisonType, SceneNodeFlags filter)
        {
            var current = Parent;

            while (current != null)
            {
                if (current.Name.Equals(name, comparisonType) && current.MatchesFilter(filter))
                    return true;

                current = current.Parent;
            }

            return false;
        }

        /// <summary>
        /// Enumerates all descendant nodes in depth-first order.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{SceneNode}"/> yielding descendant nodes.</returns>
        public IEnumerable<SceneNode> EnumerateDescendants() => EnumerateDescendants(SceneNodeFlags.None);

        /// <summary>
        /// Enumerates all descendant nodes in depth-first order that match the provided filter.
        /// </summary>
        /// <param name="filter">The flags descendant nodes must contain to be returned.</param>
        /// <returns>An <see cref="IEnumerable{SceneNode}"/> yielding descendant nodes.</returns>
        public IEnumerable<SceneNode> EnumerateDescendants(SceneNodeFlags filter)
        {
            if (_children is null)
                yield break;

            foreach (var child in _children)
            {
                if (child.MatchesFilter(filter))
                    yield return child;

                foreach (var descendant in child.EnumerateDescendants(filter))
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>
        /// Enumerates ancestor nodes from the immediate parent up to the root.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{SceneNode}"/> yielding ancestor nodes.</returns>
        public IEnumerable<SceneNode> EnumerateAncestors() => EnumerateAncestors(SceneNodeFlags.None);

        /// <summary>
        /// Enumerates ancestor nodes from the immediate parent up to the root that match the provided filter.
        /// </summary>
        /// <param name="filter">The flags ancestor nodes must contain to be returned.</param>
        /// <returns>An <see cref="IEnumerable{SceneNode}"/> yielding ancestor nodes.</returns>
        public IEnumerable<SceneNode> EnumerateAncestors(SceneNodeFlags filter)
        {
            var current = Parent;

            while (current != null)
            {
                if (current.MatchesFilter(filter))
                    yield return current;

                current = current.Parent;
            }
        }

        /// <summary>
        /// Enumerates direct child nodes of this node.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{SceneNode}"/> of child nodes (empty if none).</returns>
        public IEnumerable<SceneNode> EnumerateChildren() => EnumerateChildren(SceneNodeFlags.None);

        /// <summary>
        /// Enumerates direct child nodes of this node that match the provided filter.
        /// </summary>
        /// <param name="filter">The flags child nodes must contain to be returned.</param>
        /// <returns>An <see cref="IEnumerable{SceneNode}"/> of child nodes (empty if none).</returns>
        public IEnumerable<SceneNode> EnumerateChildren(SceneNodeFlags filter) =>
            _children?.Where(x => x.MatchesFilter(filter)) ?? Enumerable.Empty<SceneNode>();

        /// <summary>
        /// Enumerates descendant nodes of the specified type.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <returns>An <see cref="IEnumerable{T}"/> yielding matching descendant nodes.</returns>
        public IEnumerable<T> EnumerateDescendants<T>() where T : SceneNode =>
            EnumerateDescendants<T>(SceneNodeFlags.None);

        /// <summary>
        /// Enumerates descendant nodes of the specified type that match the provided filter.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <param name="filter">The flags descendant nodes must contain to be returned.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> yielding matching descendant nodes.</returns>
        public IEnumerable<T> EnumerateDescendants<T>(SceneNodeFlags filter) where T : SceneNode
        {
            foreach (var descendant in EnumerateDescendants(filter))
            {
                if (descendant.GetType() == typeof(T))
                    yield return (T)descendant;
            }
        }

        /// <summary>
        /// Enumerates ancestor nodes of the specified type.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <returns>An <see cref="IEnumerable{T}"/> yielding matching ancestor nodes.</returns>
        public IEnumerable<T> EnumerateAncestors<T>() where T : SceneNode =>
            EnumerateAncestors(SceneNodeFlags.None).OfType<T>();

        /// <summary>
        /// Enumerates ancestor nodes of the specified type that match the provided filter.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <param name="filter">The flags ancestor nodes must contain to be returned.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> yielding matching ancestor nodes.</returns>
        public IEnumerable<T> EnumerateAncestors<T>(SceneNodeFlags filter) where T : SceneNode =>
            EnumerateAncestors(filter).OfType<T>();

        /// <summary>
        /// Enumerates child nodes of the specified type.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <returns>An <see cref="IEnumerable{T}"/> yielding matching child nodes.</returns>
        public IEnumerable<T> EnumerateChildren<T>() where T : SceneNode =>
            EnumerateChildren(SceneNodeFlags.None).OfType<T>();

        /// <summary>
        /// Enumerates child nodes of the specified type that match the provided filter.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <param name="filter">The flags child nodes must contain to be returned.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> yielding matching child nodes.</returns>
        public IEnumerable<T> EnumerateChildren<T>(SceneNodeFlags filter) where T : SceneNode =>
            EnumerateChildren(filter).OfType<T>();

        /// <summary>
        /// Returns an array containing all descendant nodes.
        /// </summary>
        public SceneNode[] GetDescendants() => GetDescendants(SceneNodeFlags.None);

        /// <summary>
        /// Returns an array containing all descendant nodes that match the provided filter.
        /// </summary>
        /// <param name="filter">The flags descendant nodes must contain to be returned.</param>
        public SceneNode[] GetDescendants(SceneNodeFlags filter) => [.. EnumerateDescendants(filter)];

        /// <summary>
        /// Returns an array containing all ancestor nodes.
        /// </summary>
        public SceneNode[] GetAncestors() => GetAncestors(SceneNodeFlags.None);

        /// <summary>
        /// Returns an array containing all ancestor nodes that match the provided filter.
        /// </summary>
        /// <param name="filter">The flags ancestor nodes must contain to be returned.</param>
        public SceneNode[] GetAncestors(SceneNodeFlags filter) => [.. EnumerateAncestors(filter)];

        /// <summary>
        /// Returns an array containing all descendant nodes of the specified type.
        /// </summary>
        public T[] GetDescendants<T>() where T : SceneNode => GetDescendants<T>(SceneNodeFlags.None);

        /// <summary>
        /// Returns an array containing all descendant nodes of the specified type that match the provided filter.
        /// </summary>
        /// <param name="filter">The flags descendant nodes must contain to be returned.</param>
        public T[] GetDescendants<T>(SceneNodeFlags filter) where T : SceneNode => [.. EnumerateDescendants<T>(filter)];

        /// <summary>
        /// Returns an array containing all ancestor nodes of the specified type.
        /// </summary>
        public T[] GetAncestors<T>() where T : SceneNode => GetAncestors<T>(SceneNodeFlags.None);

        /// <summary>
        /// Returns an array containing all ancestor nodes of the specified type that match the provided filter.
        /// </summary>
        /// <param name="filter">The flags ancestor nodes must contain to be returned.</param>
        public T[] GetAncestors<T>(SceneNodeFlags filter) where T : SceneNode => [.. EnumerateAncestors<T>(filter)];

        /// <summary>
        /// Attempts to find a direct child with the specified name using current culture comparison.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="node">The matching child node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching child was found; otherwise <c>false</c>.</returns>
        public bool TryFindChild(string name, [NotNullWhen(true)] out SceneNode? node) =>
            TryFindChild(name, StringComparison.CurrentCulture, SceneNodeFlags.None, out node);

        /// <summary>
        /// Attempts to find a direct child with the specified name and filter using current culture comparison.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="filter">The flags the matching child must contain.</param>
        /// <param name="node">The matching child node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching child was found; otherwise <c>false</c>.</returns>
        public bool TryFindChild(string name, SceneNodeFlags filter, [NotNullWhen(true)] out SceneNode? node) =>
            TryFindChild(name, StringComparison.CurrentCulture, filter, out node);

        /// <summary>
        /// Attempts to find a direct child with the specified name using the provided comparison.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The comparison to use.</param>
        /// <param name="node">The matching child node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching child was found; otherwise <c>false</c>.</returns>
        public bool TryFindChild(string name, StringComparison comparisonType, [NotNullWhen(true)] out SceneNode? node) =>
            TryFindChild(name, comparisonType, SceneNodeFlags.None, out node);

        /// <summary>
        /// Attempts to find a direct child with the specified name using the provided comparison and filter.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The comparison to use.</param>
        /// <param name="filter">The flags the matching child must contain.</param>
        /// <param name="node">The matching child node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching child was found; otherwise <c>false</c>.</returns>
        public bool TryFindChild(string name, StringComparison comparisonType, SceneNodeFlags filter, [NotNullWhen(true)] out SceneNode? node)
        {
            node = EnumerateChildren(filter).FirstOrDefault(x => x.Name.Equals(name, comparisonType));
            return node is not null;
        }

        /// <summary>
        /// Attempts to find a descendant with the specified name using current culture comparison.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="node">The matching descendant node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching descendant was found; otherwise <c>false</c>.</returns>
        public bool TryFindDescendant(string name, [NotNullWhen(true)] out SceneNode? node) =>
            TryFindDescendant(name, StringComparison.CurrentCulture, SceneNodeFlags.None, out node);

        /// <summary>
        /// Attempts to find a descendant with the specified name and filter using current culture comparison.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="filter">The flags the matching descendant must contain.</param>
        /// <param name="node">The matching descendant node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching descendant was found; otherwise <c>false</c>.</returns>
        public bool TryFindDescendant(string name, SceneNodeFlags filter, [NotNullWhen(true)] out SceneNode? node) =>
            TryFindDescendant(name, StringComparison.CurrentCulture, filter, out node);

        /// <summary>
        /// Attempts to find a descendant with the specified name using the provided comparison.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The comparison to use.</param>
        /// <param name="node">The matching descendant node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching descendant was found; otherwise <c>false</c>.</returns>
        public bool TryFindDescendant(string name, StringComparison comparisonType, [NotNullWhen(true)] out SceneNode? node) =>
            TryFindDescendant(name, comparisonType, SceneNodeFlags.None, out node);

        /// <summary>
        /// Attempts to find a descendant with the specified name using the provided comparison and filter.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The comparison to use.</param>
        /// <param name="filter">The flags the matching descendant must contain.</param>
        /// <param name="node">The matching descendant node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching descendant was found; otherwise <c>false</c>.</returns>
        public bool TryFindDescendant(string name, StringComparison comparisonType, SceneNodeFlags filter, [NotNullWhen(true)] out SceneNode? node)
        {
            node = EnumerateDescendants(filter).FirstOrDefault(x => x.Name.Equals(name, comparisonType));
            return node is not null;
        }

        /// <summary>
        /// Attempts to find an ancestor with the specified name using current culture comparison.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="node">The matching ancestor node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching ancestor was found; otherwise <c>false</c>.</returns>
        public bool TryFindAncestor(string name, [NotNullWhen(true)] out SceneNode? node) =>
            TryFindAncestor(name, StringComparison.CurrentCulture, SceneNodeFlags.None, out node);

        /// <summary>
        /// Attempts to find an ancestor with the specified name and filter using current culture comparison.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="filter">The flags the matching ancestor must contain.</param>
        /// <param name="node">The matching ancestor node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching ancestor was found; otherwise <c>false</c>.</returns>
        public bool TryFindAncestor(string name, SceneNodeFlags filter, [NotNullWhen(true)] out SceneNode? node) =>
            TryFindAncestor(name, StringComparison.CurrentCulture, filter, out node);

        /// <summary>
        /// Attempts to find an ancestor with the specified name using the provided comparison.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The comparison to use.</param>
        /// <param name="node">The matching ancestor node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching ancestor was found; otherwise <c>false</c>.</returns>
        public bool TryFindAncestor(string name, StringComparison comparisonType, [NotNullWhen(true)] out SceneNode? node) =>
            TryFindAncestor(name, comparisonType, SceneNodeFlags.None, out node);

        /// <summary>
        /// Attempts to find an ancestor with the specified name using the provided comparison and filter.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The comparison to use.</param>
        /// <param name="filter">The flags the matching ancestor must contain.</param>
        /// <param name="node">The matching ancestor node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching ancestor was found; otherwise <c>false</c>.</returns>
        public bool TryFindAncestor(string name, StringComparison comparisonType, SceneNodeFlags filter, [NotNullWhen(true)] out SceneNode? node)
        {
            node = EnumerateAncestors(filter).FirstOrDefault(x => x.Name.Equals(name, comparisonType));
            return node is not null;
        }

        /// <summary>
        /// Attempts to find a direct child of the specified type and name using current culture comparison.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="node">The matching child node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching child was found; otherwise <c>false</c>.</returns>
        public bool TryFindChild<T>(string name, [NotNullWhen(true)] out T? node) where T : SceneNode =>
            TryFindChild(name, StringComparison.CurrentCulture, SceneNodeFlags.None, out node);

        /// <summary>
        /// Attempts to find a direct child of the specified type and name using current culture comparison and filter.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="filter">The flags the matching child must contain.</param>
        /// <param name="node">The matching child node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching child was found; otherwise <c>false</c>.</returns>
        public bool TryFindChild<T>(string name, SceneNodeFlags filter, [NotNullWhen(true)] out T? node) where T : SceneNode =>
            TryFindChild(name, StringComparison.CurrentCulture, filter, out node);

        /// <summary>
        /// Attempts to find a direct child of the specified type and name using the provided comparison.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The comparison to use.</param>
        /// <param name="node">The matching child node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching child was found; otherwise <c>false</c>.</returns>
        public bool TryFindChild<T>(string name, StringComparison comparisonType, [NotNullWhen(true)] out T? node) where T : SceneNode =>
            TryFindChild(name, comparisonType, SceneNodeFlags.None, out node);

        /// <summary>
        /// Attempts to find a direct child of the specified type and name using the provided comparison and filter.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The comparison to use.</param>
        /// <param name="filter">The flags the matching child must contain.</param>
        /// <param name="node">The matching child node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching child was found; otherwise <c>false</c>.</returns>
        public bool TryFindChild<T>(string name, StringComparison comparisonType, SceneNodeFlags filter, [NotNullWhen(true)] out T? node) where T : SceneNode
        {
            node = EnumerateChildren<T>(filter).FirstOrDefault(x => x.Name.Equals(name, comparisonType));
            return node is not null;
        }

        /// <summary>
        /// Attempts to find a descendant of the specified type and name using current culture comparison.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="node">The matching descendant node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching descendant was found; otherwise <c>false</c>.</returns>
        public bool TryFindDescendant<T>(string name, [NotNullWhen(true)] out T? node) where T : SceneNode =>
            TryFindDescendant(name, StringComparison.CurrentCulture, SceneNodeFlags.None, out node);

        /// <summary>
        /// Attempts to find a descendant of the specified type and name using current culture comparison and filter.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="filter">The flags the matching descendant must contain.</param>
        /// <param name="node">The matching descendant node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching descendant was found; otherwise <c>false</c>.</returns>
        public bool TryFindDescendant<T>(string name, SceneNodeFlags filter, [NotNullWhen(true)] out T? node) where T : SceneNode =>
            TryFindDescendant(name, StringComparison.CurrentCulture, filter, out node);

        /// <summary>
        /// Attempts to find a descendant of the specified type and name using the provided comparison.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The comparison to use.</param>
        /// <param name="node">The matching descendant node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching descendant was found; otherwise <c>false</c>.</returns>
        public bool TryFindDescendant<T>(string name, StringComparison comparisonType, [NotNullWhen(true)] out T? node) where T : SceneNode =>
            TryFindDescendant(name, comparisonType, SceneNodeFlags.None, out node);

        /// <summary>
        /// Attempts to find a descendant of the specified type and name using the provided comparison and filter.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The comparison to use.</param>
        /// <param name="filter">The flags the matching descendant must contain.</param>
        /// <param name="node">The matching descendant node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching descendant was found; otherwise <c>false</c>.</returns>
        public bool TryFindDescendant<T>(string name, StringComparison comparisonType, SceneNodeFlags filter, [NotNullWhen(true)] out T? node) where T : SceneNode
        {
            node = EnumerateDescendants<T>(filter).FirstOrDefault(x => x.Name.Equals(name, comparisonType));
            return node is not null;
        }

        /// <summary>
        /// Attempts to find an ancestor of the specified type and name using current culture comparison.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="node">The matching ancestor node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching ancestor was found; otherwise <c>false</c>.</returns>
        public bool TryFindAncestor<T>(string name, [NotNullWhen(true)] out T? node) where T : SceneNode =>
            TryFindAncestor(name, StringComparison.CurrentCulture, SceneNodeFlags.None, out node);

        /// <summary>
        /// Attempts to find an ancestor of the specified type and name using current culture comparison and filter.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="filter">The flags the matching ancestor must contain.</param>
        /// <param name="node">The matching ancestor node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching ancestor was found; otherwise <c>false</c>.</returns>
        public bool TryFindAncestor<T>(string name, SceneNodeFlags filter, [NotNullWhen(true)] out T? node) where T : SceneNode =>
            TryFindAncestor(name, StringComparison.CurrentCulture, filter, out node);

        /// <summary>
        /// Attempts to find an ancestor of the specified type and name using the provided comparison.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The comparison to use.</param>
        /// <param name="node">The matching ancestor node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching ancestor was found; otherwise <c>false</c>.</returns>
        private bool TryFindAncestor<T>(string name, StringComparison comparisonType, [NotNullWhen(true)] out T? node) where T : SceneNode =>
            TryFindAncestor(name, comparisonType, SceneNodeFlags.None, out node);

        /// <summary>
        /// Attempts to find an ancestor of the specified type and name using the provided comparison and filter.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The comparison to use.</param>
        /// <param name="filter">The flags the matching ancestor must contain.</param>
        /// <param name="node">The matching ancestor node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching ancestor was found; otherwise <c>false</c>.</returns>
        private bool TryFindAncestor<T>(string name, StringComparison comparisonType, SceneNodeFlags filter, [NotNullWhen(true)] out T? node) where T : SceneNode
        {
            node = EnumerateAncestors<T>(filter).FirstOrDefault(x => x.Name.Equals(name, comparisonType));
            return node is not null;
        }

        /// <summary>
        /// Finds a direct child by name using current culture comparison.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <returns>The matching child node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching child is found.</exception>
        public SceneNode FindChild(string name) => FindChild(name, StringComparison.CurrentCulture, SceneNodeFlags.None);

        /// <summary>
        /// Finds a direct child by name using current culture comparison and the provided filter.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="filter">The flags the matching child must contain.</param>
        /// <returns>The matching child node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching child is found.</exception>
        public SceneNode FindChild(string name, SceneNodeFlags filter) => FindChild(name, StringComparison.CurrentCulture, filter);

        /// <summary>
        /// Finds a direct child by name using the specified comparison.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The string comparison to use.</param>
        /// <returns>The matching child node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching child is found.</exception>
        public SceneNode FindChild(string name, StringComparison comparisonType) =>
            FindChild(name, comparisonType, SceneNodeFlags.None);

        /// <summary>
        /// Finds a direct child by name using the specified comparison and filter.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The string comparison to use.</param>
        /// <param name="filter">The flags the matching child must contain.</param>
        /// <returns>The matching child node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching child is found.</exception>
        public SceneNode FindChild(string name, StringComparison comparisonType, SceneNodeFlags filter)
        {
            if (TryFindChild(name, comparisonType, filter, out var node))
                return node;

            throw new SceneNodeNotFoundException($"A child with the name: {name} was not found in: {Name}");
        }

        /// <summary>
        /// Finds a descendant by name using current culture comparison.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <returns>The matching descendant node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching descendant is found.</exception>
        public SceneNode FindDescendant(string name) => FindDescendant(name, StringComparison.CurrentCulture, SceneNodeFlags.None);

        /// <summary>
        /// Finds a descendant by name using current culture comparison and the provided filter.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="filter">The flags the matching descendant must contain.</param>
        /// <returns>The matching descendant node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching descendant is found.</exception>
        public SceneNode FindDescendant(string name, SceneNodeFlags filter) =>
            FindDescendant(name, StringComparison.CurrentCulture, filter);

        /// <summary>
        /// Finds a descendant by name using the specified comparison.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The string comparison to use.</param>
        /// <returns>The matching descendant node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching descendant is found.</exception>
        public SceneNode FindDescendant(string name, StringComparison comparisonType) =>
            FindDescendant(name, comparisonType, SceneNodeFlags.None);

        /// <summary>
        /// Finds a descendant by name using the specified comparison and filter.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The string comparison to use.</param>
        /// <param name="filter">The flags the matching descendant must contain.</param>
        /// <returns>The matching descendant node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching descendant is found.</exception>
        public SceneNode FindDescendant(string name, StringComparison comparisonType, SceneNodeFlags filter)
        {
            if (TryFindDescendant(name, comparisonType, filter, out var node))
                return node;

            throw new SceneNodeNotFoundException($"A descendant with the name: {name} was not found in: {Name}");
        }

        /// <summary>
        /// Finds an ancestor by name using current culture comparison.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <returns>The matching ancestor node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching ancestor is found.</exception>
        public SceneNode FindAncestor(string name) => FindAncestor(name, StringComparison.CurrentCulture, SceneNodeFlags.None);

        /// <summary>
        /// Finds an ancestor by name using current culture comparison and the provided filter.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="filter">The flags the matching ancestor must contain.</param>
        /// <returns>The matching ancestor node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching ancestor is found.</exception>
        public SceneNode FindAncestor(string name, SceneNodeFlags filter) => FindAncestor(name, StringComparison.CurrentCulture, filter);

        /// <summary>
        /// Finds an ancestor by name using the specified comparison.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The string comparison to use.</param>
        /// <returns>The matching ancestor node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching ancestor is found.</exception>
        public SceneNode FindAncestor(string name, StringComparison comparisonType) =>
            FindAncestor(name, comparisonType, SceneNodeFlags.None);

        /// <summary>
        /// Finds an ancestor by name using the specified comparison and filter.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The string comparison to use.</param>
        /// <param name="filter">The flags the matching ancestor must contain.</param>
        /// <returns>The matching ancestor node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching ancestor is found.</exception>
        public SceneNode FindAncestor(string name, StringComparison comparisonType, SceneNodeFlags filter)
        {
            if (TryFindAncestor(name, comparisonType, filter, out var node))
                return node;

            throw new SceneNodeNotFoundException($"An ancestor with the name: {name} was not found in: {Name}");
        }

        /// <summary>
        /// Finds a direct child of the specified type and name using current culture comparison.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <returns>The matching child node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching child is found.</exception>
        public T FindChild<T>(string name) where T : SceneNode =>
            FindChild<T>(name, StringComparison.CurrentCulture, SceneNodeFlags.None);

        /// <summary>
        /// Finds a direct child of the specified type and name using current culture comparison and filter.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="filter">The flags the matching child must contain.</param>
        /// <returns>The matching child node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching child is found.</exception>
        public T FindChild<T>(string name, SceneNodeFlags filter) where T : SceneNode =>
            FindChild<T>(name, StringComparison.CurrentCulture, filter);

        /// <summary>
        /// Finds a direct child of the specified type and name using the specified comparison.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The string comparison to use.</param>
        /// <returns>The matching child node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching child is found.</exception>
        public T FindChild<T>(string name, StringComparison comparisonType) where T : SceneNode =>
            FindChild<T>(name, comparisonType, SceneNodeFlags.None);

        /// <summary>
        /// Finds a direct child of the specified type and name using the specified comparison and filter.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The string comparison to use.</param>
        /// <param name="filter">The flags the matching child must contain.</param>
        /// <returns>The matching child node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching child is found.</exception>
        public T FindChild<T>(string name, StringComparison comparisonType, SceneNodeFlags filter) where T : SceneNode
        {
            if (TryFindChild<T>(name, comparisonType, filter, out var node))
                return node;

            throw new SceneNodeNotFoundException($"A child with the name: {name} of type: {typeof(T)} was not found in: {Name}");
        }

        /// <summary>
        /// Finds a descendant of the specified type and name using current culture comparison.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <returns>The matching descendant node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching descendant is found.</exception>
        public T FindDescendant<T>(string name) where T : SceneNode =>
            FindDescendant<T>(name, StringComparison.CurrentCulture, SceneNodeFlags.None);

        /// <summary>
        /// Finds a descendant of the specified type and name using current culture comparison and filter.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="filter">The flags the matching descendant must contain.</param>
        /// <returns>The matching descendant node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching descendant is found.</exception>
        public T FindDescendant<T>(string name, SceneNodeFlags filter) where T : SceneNode =>
            FindDescendant<T>(name, StringComparison.CurrentCulture, filter);

        /// <summary>
        /// Finds a descendant of the specified type and name using the specified comparison.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The string comparison to use.</param>
        /// <returns>The matching descendant node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching descendant is found.</exception>
        public T FindDescendant<T>(string name, StringComparison comparisonType) where T : SceneNode =>
            FindDescendant<T>(name, comparisonType, SceneNodeFlags.None);

        /// <summary>
        /// Finds a descendant of the specified type and name using the specified comparison and filter.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The string comparison to use.</param>
        /// <param name="filter">The flags the matching descendant must contain.</param>
        /// <returns>The matching descendant node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching descendant is found.</exception>
        public T FindDescendant<T>(string name, StringComparison comparisonType, SceneNodeFlags filter) where T : SceneNode
        {
            if (TryFindDescendant<T>(name, comparisonType, filter, out var node))
                return node;

            throw new SceneNodeNotFoundException($"A descendant with the name: {name} of type: {typeof(T)} was not found in: {Name}");
        }

        /// <summary>
        /// Finds an ancestor of the specified type and name using current culture comparison.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <returns>The matching ancestor node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching ancestor is found.</exception>
        public T FindAncestor<T>(string name) where T : SceneNode =>
            FindAncestor<T>(name, StringComparison.CurrentCulture, SceneNodeFlags.None);

        /// <summary>
        /// Finds an ancestor of the specified type and name using current culture comparison and filter.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="filter">The flags the matching ancestor must contain.</param>
        /// <returns>The matching ancestor node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching ancestor is found.</exception>
        public T FindAncestor<T>(string name, SceneNodeFlags filter) where T : SceneNode =>
            FindAncestor<T>(name, StringComparison.CurrentCulture, filter);

        /// <summary>
        /// Finds an ancestor of the specified type and name using the specified comparison.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The string comparison to use.</param>
        /// <returns>The matching ancestor node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching ancestor is found.</exception>
        public T FindAncestor<T>(string name, StringComparison comparisonType) where T : SceneNode =>
            FindAncestor<T>(name, comparisonType, SceneNodeFlags.None);

        /// <summary>
        /// Finds an ancestor of the specified type and name using the specified comparison and filter.
        /// </summary>
        /// <typeparam name="T">The node type.</typeparam>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The string comparison to use.</param>
        /// <param name="filter">The flags the matching ancestor must contain.</param>
        /// <returns>The matching ancestor node.</returns>
        /// <exception cref="SceneNodeNotFoundException">Thrown when no matching ancestor is found.</exception>
        public T FindAncestor<T>(string name, StringComparison comparisonType, SceneNodeFlags filter) where T : SceneNode
        {
            if (TryFindAncestor<T>(name, comparisonType, filter, out var node))
                return node;

            throw new SceneNodeNotFoundException($"An ancestor with the name: {name} of type: {typeof(T)} was not found in: {Name}");
        }

        /// <summary>
        /// Removes a child node by reference.
        /// </summary>
        /// <param name="node">The node to remove.</param>
        /// <returns>True if the node was removed; otherwise false.</returns>
        public bool RemoveNode(SceneNode node)
        {
            if (_children is null || !_children.Remove(node))
                return false;
            node.Parent = null;
            OnChildRemoved(node);
            return true;
        }

        /// <summary>
        /// Removes a child node by name.
        /// </summary>
        /// <param name="name">The name of the node to remove.</param>
        /// <returns>True if the node was removed; otherwise false.</returns>
        public bool RemoveNode(string name)
        {
            var child = EnumerateChildren().FirstOrDefault(x => x.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            if (child == null)
                return false;
            return RemoveNode(child);
        }

        /// <summary>
        /// Removes all child nodes.
        /// </summary>
        public void ClearNodes()
        {
            if (_children is null)
                return;
            foreach (var child in _children)
            {
                child.Parent = null;
                OnChildRemoved(child);
            }
            _children.Clear();
        }

        /// <summary>
        /// Called when a child node is added.
        /// </summary>
        protected virtual void OnChildAdded(SceneNode child) { }

        /// <summary>
        /// Called when a child node is removed.
        /// </summary>
        protected virtual void OnChildRemoved(SceneNode child) { }

        /// <summary>
        /// Adds the specified node as a child of this scene node.
        /// </summary>
        /// <remarks>After the node is added, its Parent property is set to this node and its scene
        /// reference is updated. The method returns the same instance that was added.</remarks>
        /// <typeparam name="T">The type of the scene node to add. Must derive from SceneNode.</typeparam>
        /// <param name="node">The node to add as a child. The node must not already have a parent, and its name must be unique among this
        /// node's children.</param>
        /// <returns>The node that was added as a child.</returns>
        /// <exception cref="SceneNodeParentException">Thrown if the specified node already has a parent.</exception>
        /// <exception cref="SceneNodeDuplicateException">Thrown if a child node with the same name already exists in this node.</exception>
        public T AddNode<T>(T node) where T : SceneNode
        {
            if (node.Parent != null)
                throw new SceneNodeParentException($"Node '{node.Name}' already has a parent.");
            _children ??= [];
            if (_children.Any(x => x.Name.Equals(node.Name, StringComparison.CurrentCultureIgnoreCase)))
                throw new SceneNodeDuplicateException($"A node with the name: {node.Name} already exists in: {Name}");
            _children.Add(node);

            node.Parent = this;
            node._scene = _scene;

            OnChildAdded(node);
            return node;
        }

        /// <summary>
        /// Adds a new node of type T as a child.
        /// </summary>
        /// <typeparam name="T">The type of the scene node to add. Must have a parameterless constructor.</typeparam>
        /// <returns>The newly created and added node.</returns>
        public T AddNode<T>() where T : SceneNode, new() => AddNode(new T());

        /// <summary>
        /// Adds a new node of type T with the specified name as a child.
        /// </summary>
        /// <typeparam name="T">The type of the scene node to add. Must have a parameterless constructor.</typeparam>
        /// <param name="name">The name to assign to the new node.</param>
        /// <returns>The newly created and added node.</returns>
        public T AddNode<T>(string name) where T : SceneNode, new()
        {
            var node = AddNode(new T());
            node.Name = name;
            return node;
        }

        /// <summary>
        /// Updates the node and all its descendants.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
        public void Update(float deltaTime)
        {
            if (Flags.HasFlag(SceneNodeFlags.NoUpdate))
                return;

            OnUpdate(deltaTime);

            _children?.ForEach(x => x.Update(deltaTime));
        }

        /// <summary>
        /// Called when the node is updated. Override to implement custom update logic.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
        protected virtual void OnUpdate(float deltaTime)
        {
        }

        /// <summary>
        /// Gets the first node of the specified type, starting from this node.
        /// </summary>
        /// <typeparam name="T">The node type to find.</typeparam>
        /// <returns>The first matching node.</returns>
        public T FirstOfType<T>() where T : SceneNode => FirstOfType<T>(SceneNodeFlags.None);

        /// <summary>
        /// Gets the first node of the specified type, starting from this node, that matches the provided filter.
        /// </summary>
        /// <typeparam name="T">The node type to find.</typeparam>
        /// <param name="filter">The flags the matching node must contain.</param>
        /// <returns>The first matching node.</returns>
        public T FirstOfType<T>(SceneNodeFlags filter) where T : SceneNode
        {
            if (this is T tSelf && MatchesFilter(filter))
                return tSelf;
            var found = EnumerateDescendants<T>(filter).FirstOrDefault();
            return found is null ? throw new SceneNodeNotFoundException($"No node of type {typeof(T)} found in: {Name}") : found;
        }

        /// <summary>
        /// Attempts to get the first node of the specified type, starting from this node.
        /// </summary>
        /// <typeparam name="T">The node type to find.</typeparam>
        /// <returns>The first matching node, or null if not found.</returns>
        public T? TryGetFirstOfType<T>() where T : SceneNode => TryGetFirstOfType<T>(SceneNodeFlags.None);

        /// <summary>
        /// Attempts to get the first node of the specified type, starting from this node, that matches the provided filter.
        /// </summary>
        /// <typeparam name="T">The node type to find.</typeparam>
        /// <param name="filter">The flags the matching node must contain.</param>
        /// <returns>The first matching node, or null if not found.</returns>
        public T? TryGetFirstOfType<T>(SceneNodeFlags filter) where T : SceneNode
        {
            if (this is T tSelf && MatchesFilter(filter))
            {
                return tSelf;
            }

            return EnumerateDescendants<T>(filter).FirstOrDefault();
        }

        /// <summary>
        /// Attempts to get the first node of the specified type, starting from this node.
        /// </summary>
        /// <typeparam name="T">The node type to find.</typeparam>
        /// <param name="node">The found node, or null if not found.</param>
        /// <returns>True if a matching node was found; otherwise false.</returns>
        public bool TryGetFirstOfType<T>([NotNullWhen(true)] out T? node) where T : SceneNode =>
            TryGetFirstOfType(SceneNodeFlags.None, out node);

        /// <summary>
        /// Attempts to get the first node of the specified type, starting from this node, that matches the provided filter.
        /// </summary>
        /// <typeparam name="T">The node type to find.</typeparam>
        /// <param name="filter">The flags the matching node must contain.</param>
        /// <param name="node">The found node, or null if not found.</param>
        /// <returns>True if a matching node was found; otherwise false.</returns>
        public bool TryGetFirstOfType<T>(SceneNodeFlags filter, [NotNullWhen(true)] out T? node) where T : SceneNode
        {
            if (this is T tSelf && MatchesFilter(filter))
            {
                node = tSelf;
                return true;
            }
            node = EnumerateDescendants<T>(filter).FirstOrDefault();
            return node is not null;
        }

        /// <summary>
        /// Finds a node by its path.
        /// </summary>
        /// <param name="path">The path to the node (e.g., "Root/Child/Grandchild").</param>
        /// <returns>The node at the specified path.</returns>
        /// <exception cref="ArgumentException">Thrown if path is null or empty.</exception>
        /// <exception cref="SceneNodeNotFoundException">Thrown if the node at the path is not found.</exception>
        public SceneNode FindByPath(string path) => FindByPath(path, SceneNodeFlags.None);

        /// <summary>
        /// Finds a node by its path and requires the final node to match the provided filter.
        /// </summary>
        /// <param name="path">The path to the node (e.g., "Root/Child/Grandchild").</param>
        /// <param name="filter">The flags the final node must contain.</param>
        /// <returns>The node at the specified path.</returns>
        /// <exception cref="ArgumentException">Thrown if path is null or empty.</exception>
        /// <exception cref="SceneNodeNotFoundException">Thrown if the node at the path is not found.</exception>
        public SceneNode FindByPath(string path, SceneNodeFlags filter)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            SceneNode? current = this;
            if (!segments[0].Equals(Name, StringComparison.CurrentCultureIgnoreCase))
                throw new SceneNodeNotFoundException($"Path root '{segments[0]}' does not match node '{Name}'.");
            for (int i = 1; i < segments.Length && current != null; i++)
            {
                current = current.EnumerateChildren().FirstOrDefault(n => n.Name.Equals(segments[i], StringComparison.CurrentCultureIgnoreCase));
            }
            if (current == null || !current.MatchesFilter(filter))
                throw new SceneNodeNotFoundException($"Node at path '{path}' not found.");
            return current;
        }

        /// <summary>
        /// Attempts to find a node by its path.
        /// </summary>
        /// <param name="path">The path to the node (e.g., "Root/Child/Grandchild").</param>
        /// <param name="node">The found node, or null if not found.</param>
        /// <returns>True if the node was found; otherwise false.</returns>
        public bool TryFindByPath(string path, [NotNullWhen(true)] out SceneNode? node) =>
            TryFindByPath(path, SceneNodeFlags.None, out node);

        /// <summary>
        /// Attempts to find a node by its path and requires the final node to match the provided filter.
        /// </summary>
        /// <param name="path">The path to the node (e.g., "Root/Child/Grandchild").</param>
        /// <param name="filter">The flags the final node must contain.</param>
        /// <param name="node">The found node, or null if not found.</param>
        /// <returns>True if the node was found; otherwise false.</returns>
        public bool TryFindByPath(string path, SceneNodeFlags filter, [NotNullWhen(true)] out SceneNode? node)
        {
            node = null;
            if (string.IsNullOrWhiteSpace(path))
                return false;
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            SceneNode? current = this;
            if (!segments[0].Equals(Name, StringComparison.CurrentCultureIgnoreCase))
                return false;
            for (int i = 1; i < segments.Length && current != null; i++)
            {
                current = current.EnumerateChildren().FirstOrDefault(n => n.Name.Equals(segments[i], StringComparison.CurrentCultureIgnoreCase));
            }
            if (current == null || !current.MatchesFilter(filter))
                return false;
            node = current;
            return true;
        }

        /// <summary>
        /// Gets the depth of this node in the hierarchy.
        /// </summary>
        /// <returns>The depth (0 if this is a root node).</returns>
        public int GetDepth()
        {
            int depth = 0;
            var current = Parent;
            while (current != null)
            {
                depth++;
                current = current.Parent;
            }
            return depth;
        }

        /// <summary>
        /// Enumerates sibling nodes (nodes with the same parent).
        /// </summary>
        /// <returns>An <see cref="IEnumerable{SceneNode}"/> yielding sibling nodes.</returns>
        public IEnumerable<SceneNode> EnumerateSiblings() => EnumerateSiblings(SceneNodeFlags.None);

        /// <summary>
        /// Enumerates sibling nodes (nodes with the same parent) that match the provided filter.
        /// </summary>
        /// <param name="filter">The flags sibling nodes must contain to be returned.</param>
        /// <returns>An <see cref="IEnumerable{SceneNode}"/> yielding sibling nodes.</returns>
        public IEnumerable<SceneNode> EnumerateSiblings(SceneNodeFlags filter) =>
            Parent?.EnumerateChildren(filter).Where(n => n != this) ?? [];

        /// <summary>
        /// Returns an array containing all sibling nodes.
        /// </summary>
        public SceneNode[] GetSiblings() => GetSiblings(SceneNodeFlags.None);

        /// <summary>
        /// Returns an array containing all sibling nodes that match the provided filter.
        /// </summary>
        /// <param name="filter">The flags sibling nodes must contain to be returned.</param>
        public SceneNode[] GetSiblings(SceneNodeFlags filter) => [.. EnumerateSiblings(filter)];

        /// <summary>
        /// Detaches this node from its parent.
        /// </summary>
        public void Detach()
        {
            Parent?.RemoveNode(this);
        }

        /// <summary>
        /// Traverses this node and all descendants, performing an action on each.
        /// </summary>
        /// <param name="action">The action to perform on each node.</param>
        public void Traverse(Action<SceneNode> action) => Traverse(action, SceneNodeFlags.None);

        /// <summary>
        /// Traverses this node and all descendants that match the provided filter, performing an action on each.
        /// </summary>
        /// <param name="action">The action to perform on each node.</param>
        /// <param name="filter">The flags nodes must contain to be visited.</param>
        public void Traverse(Action<SceneNode> action, SceneNodeFlags filter)
        {
            foreach (var node in EnumerateHierarchy(filter))
                action(node);
        }

        /// <summary>
        /// Gets the root node of this node's hierarchy.
        /// </summary>
        /// <returns>The root node.</returns>
        public SceneNode GetRoot()
        {
            var current = this;
            while (current.Parent != null)
                current = current.Parent;
            return current;
        }

        /// <summary>
        /// Gets the nearest ancestor of <paramref name="node"/> that is present in <paramref name="nodes"/>.
        /// </summary>
        /// <param name="node">The node whose ancestors should be searched.</param>
        /// <param name="nodes">The candidate nodes to search for.</param>
        /// <returns>The nearest matching ancestor, or <see langword="null"/> if no ancestor is present in <paramref name="nodes"/>.</returns>
        public static SceneNode? GetBestParent(SceneNode node, SceneNode[] nodes)
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(nodes);

            if (nodes.Length == 0)
                return null;

            HashSet<SceneNode> candidates = [.. nodes];
            SceneNode? current = node.Parent;

            while (current is not null)
            {
                if (candidates.Contains(current))
                    return current;

                current = current.Parent;
            }

            return null;
        }

        /// <summary>
        /// Gets the index of the nearest ancestor of <paramref name="node"/> that is present in <paramref name="nodes"/>.
        /// </summary>
        /// <param name="node">The node whose ancestors should be searched.</param>
        /// <param name="nodes">The candidate nodes to search for.</param>
        /// <returns>The index of the nearest matching ancestor, or -1 if no ancestor is present in <paramref name="nodes"/>.</returns>
        public static int GetBestParentIndex(SceneNode node, SceneNode[] nodes)
        {
            SceneNode? parent = GetBestParent(node, nodes);
            return parent is null ? -1 : Array.IndexOf(nodes, parent);
        }

        /// <summary>
        /// Gets the bind local position.
        /// </summary>
        public Vector3 GetBindLocalPosition()
        {
            if (BindTransform.LocalPosition.HasValue)
                return BindTransform.LocalPosition.Value;

            if (!BindTransform.WorldPosition.HasValue)
            {
                BindTransform.LocalPosition = Vector3.Zero;
                return BindTransform.LocalPosition.Value;
            }

            if (Parent is not null)
            {
                BindTransform.LocalPosition = Vector3.Transform(
                    GetBindWorldPosition() - Parent.GetBindWorldPosition(),
                    Quaternion.Conjugate(Parent.GetBindWorldRotation()));
            }
            else
            {
                BindTransform.LocalPosition = GetBindWorldPosition();
            }

            return BindTransform.LocalPosition.Value;
        }

        /// <summary>
        /// Gets the bind world position.
        /// </summary>
        public Vector3 GetBindWorldPosition()
        {
            if (BindTransform.WorldPosition.HasValue)
                return BindTransform.WorldPosition.Value;

            if (!BindTransform.LocalPosition.HasValue)
                BindTransform.LocalPosition = Vector3.Zero;

            if (Parent is not null)
            {
                BindTransform.WorldPosition = Vector3.Transform(GetBindLocalPosition(), Parent.GetBindWorldRotation())
                    + Parent.GetBindWorldPosition();
            }
            else
            {
                BindTransform.WorldPosition = GetBindLocalPosition();
            }

            return BindTransform.WorldPosition.Value;
        }

        /// <summary>
        /// Gets the bind local rotation.
        /// </summary>
        public Quaternion GetBindLocalRotation()
        {
            if (BindTransform.LocalRotation.HasValue)
                return BindTransform.LocalRotation.Value;

            if (!BindTransform.WorldRotation.HasValue)
            {
                BindTransform.LocalRotation = Quaternion.Identity;
                return BindTransform.LocalRotation.Value;
            }

            if (Parent is not null)
            {
                BindTransform.LocalRotation = Quaternion.Conjugate(Parent.GetBindWorldRotation())
                    * GetBindWorldRotation();
            }
            else
            {
                BindTransform.LocalRotation = GetBindWorldRotation();
            }

            return BindTransform.LocalRotation.Value;
        }

        /// <summary>
        /// Gets the bind world rotation.
        /// </summary>
        public Quaternion GetBindWorldRotation()
        {
            if (BindTransform.WorldRotation.HasValue)
                return BindTransform.WorldRotation.Value;

            if (!BindTransform.LocalRotation.HasValue)
                BindTransform.LocalRotation = Quaternion.Identity;

            if (Parent is not null)
            {
                BindTransform.WorldRotation = Parent.GetBindWorldRotation() * GetBindLocalRotation();
            }
            else
            {
                BindTransform.WorldRotation = GetBindLocalRotation();
            }

            return BindTransform.WorldRotation.Value;
        }

        /// <summary>
        /// Gets the bind local scale.
        /// </summary>
        public Vector3 GetBindLocalScale() => BindTransform.Scale ?? Vector3.One;

        /// <summary>
        /// Gets the bind local transform matrix for this node.
        /// The matrix is composed in scale, rotation, translation order.
        /// </summary>
        /// <returns>The bind local transform matrix.</returns>
        public Matrix4x4 GetBindLocalMatrix()
        {
            return Matrix4x4.CreateScale(GetBindLocalScale())
                * Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(GetBindLocalRotation()))
                * Matrix4x4.CreateTranslation(GetBindLocalPosition());
        }

        /// <summary>
        /// Gets the bind world transform matrix for this node.
        /// This includes the full ancestor chain, not only skeletal parents.
        /// </summary>
        /// <returns>The bind world transform matrix.</returns>
        public Matrix4x4 GetBindWorldMatrix()
        {
            return Parent is not null
                ? GetBindLocalMatrix() * Parent.GetBindWorldMatrix()
                : GetBindLocalMatrix();
        }

        /// <summary>
        /// Enumerates this node followed by all its descendants in depth-first order.
        /// Useful for traversing an entire hierarchy including the root node itself.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{SceneNode}"/> yielding this node then all descendants.</returns>
        public IEnumerable<SceneNode> EnumerateHierarchy() => EnumerateHierarchy(SceneNodeFlags.None);

        /// <summary>
        /// Enumerates this node followed by all its descendants in depth-first order that match the provided filter.
        /// Useful for traversing an entire hierarchy including the root node itself.
        /// </summary>
        /// <param name="filter">The flags nodes must contain to be returned.</param>
        /// <returns>An <see cref="IEnumerable{SceneNode}"/> yielding this node then all descendants.</returns>
        public IEnumerable<SceneNode> EnumerateHierarchy(SceneNodeFlags filter)
        {
            if (MatchesFilter(filter))
                yield return this;

            foreach (var descendant in EnumerateDescendants(filter))
            {
                yield return descendant;
            }
        }

        /// <summary>
        /// Enumerates this node and all descendants of the specified type.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <returns>An <see cref="IEnumerable{T}"/> yielding matching nodes.</returns>
        public IEnumerable<T> EnumerateHierarchy<T>() where T : SceneNode =>
            EnumerateHierarchy(SceneNodeFlags.None).OfType<T>();

        /// <summary>
        /// Enumerates this node and all descendants of the specified type that match the provided filter.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <param name="filter">The flags nodes must contain to be returned.</param>
        /// <returns>An <see cref="IEnumerable{T}"/> yielding matching nodes.</returns>
        public IEnumerable<T> EnumerateHierarchy<T>(SceneNodeFlags filter) where T : SceneNode =>
            EnumerateHierarchy(filter).OfType<T>();

        /// <summary>
        /// Gets the active local position from <see cref="LiveTransform"/>, falling back
        /// to <see cref="BindTransform"/> if the live value is not set.
        /// </summary>
        /// <returns>The current local position vector.</returns>
        public Vector3 GetLiveLocalPosition()
        {
            return LiveTransform.LocalPosition ?? BindTransform.LocalPosition ?? GetBindLocalPosition();
        }

        /// <summary>
        /// Gets the active local rotation from <see cref="LiveTransform"/>, falling back
        /// to <see cref="BindTransform"/> if the live value is not set.
        /// </summary>
        /// <returns>The current local rotation quaternion.</returns>
        public Quaternion GetLiveLocalRotation()
        {
            return LiveTransform.LocalRotation ?? BindTransform.LocalRotation ?? GetBindLocalRotation();
        }

        /// <summary>
        /// Gets the active local scale from <see cref="LiveTransform"/>, falling back
        /// to <see cref="BindTransform"/> if the live value is not set.
        /// </summary>
        /// <returns>The current local scale vector, defaulting to <see cref="Vector3.One"/>.</returns>
        public Vector3 GetLiveLocalScale()
        {
            return LiveTransform.Scale ?? BindTransform.Scale ?? Vector3.One;
        }

        /// <summary>
        /// Gets the active local transform matrix for this node.
        /// The matrix is composed in scale, rotation, translation order.
        /// </summary>
        /// <returns>The active local transform matrix.</returns>
        public Matrix4x4 GetActiveLocalMatrix()
        {
            return Matrix4x4.CreateScale(GetLiveLocalScale())
                * Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(GetLiveLocalRotation()))
                * Matrix4x4.CreateTranslation(GetLiveLocalPosition());
        }

        /// <summary>
        /// Gets the active world position, preferring <see cref="LiveTransform"/> values,
        /// then <see cref="BindTransform"/>, and computing from the bind hierarchy if neither is set.
        /// </summary>
        /// <returns>The current world position vector.</returns>
        public Vector3 GetActiveWorldPosition()
        {
            return LiveTransform.WorldPosition ?? LiveTransform.LocalPosition switch
            {
                // If we have a live local but no live world, compute world from live local
                not null when Parent is not null => Vector3.Transform(LiveTransform.LocalPosition.Value, Parent.GetActiveWorldRotation()) + Parent.GetActiveWorldPosition(),
                not null => LiveTransform.LocalPosition.Value,
                _ => BindTransform.WorldPosition ?? GetBindWorldPosition()
            };
        }

        /// <summary>
        /// Gets the active world rotation, preferring <see cref="LiveTransform"/> values,
        /// then <see cref="BindTransform"/>, and computing from the bind hierarchy if neither is set.
        /// </summary>
        /// <returns>The current world rotation quaternion.</returns>
        public Quaternion GetActiveWorldRotation()
        {
            return LiveTransform.WorldRotation ?? LiveTransform.LocalRotation switch
            {
                // If we have a live local but no live world, compute world from live local
                not null when Parent is not null => Parent.GetActiveWorldRotation() * LiveTransform.LocalRotation.Value,
                not null => LiveTransform.LocalRotation.Value,
                _ => BindTransform.WorldRotation ?? GetBindWorldRotation()
            };
        }

        /// <summary>
        /// Gets the active world transform matrix for this node.
        /// This includes the full ancestor chain, not only skeletal parents.
        /// </summary>
        /// <returns>The active world transform matrix.</returns>
        public Matrix4x4 GetActiveWorldMatrix()
        {
            return Parent is not null
                ? GetActiveLocalMatrix() * Parent.GetActiveWorldMatrix()
                : GetActiveLocalMatrix();
        }

        /// <summary>
        /// Tries to compute world-space bounds for this node.
        /// The default implementation returns <see langword="false"/>; override in concrete node
        /// types that have spatial extent (e.g. <see cref="Mesh"/>, <see cref="SkeletonBone"/>).
        /// </summary>
        /// <param name="bounds">The computed bounds when successful; otherwise <see cref="SceneBounds.Invalid"/>.</param>
        /// <returns><see langword="true"/> when bounds are available; otherwise <see langword="false"/>.</returns>
        public virtual bool TryGetSceneBounds(out SceneBounds bounds)
        {
            bounds = SceneBounds.Invalid;
            return false;
        }

        /// <summary>
        /// Resets the <see cref="LiveTransform"/> back to bind pose defaults,
        /// clearing any animation or runtime modifications.
        /// </summary>
        public void ResetLiveTransform()
        {
            LiveTransform.LocalPosition = null;
            LiveTransform.LocalRotation = null;
            LiveTransform.WorldPosition = null;
            LiveTransform.WorldRotation = null;
            LiveTransform.Scale = null;
        }

        /// <summary>
        /// Disposes this node, its render handle, and all descendant nodes.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (GraphicsHandle is { } graphicsHandle)
            {
                graphicsHandle.Release();
                graphicsHandle.Dispose();
            }

            GraphicsHandle = null;

            DisposeCore();

            if (_children is not null)
            {
                for (int i = 0; i < _children.Count; i++)
                {
                    _children[i].Dispose();
                    _children[i].Parent = null;
                }

                _children.Clear();
            }

            Parent = null;
            _scene = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases node-specific resources during disposal.
        /// </summary>
        protected virtual void DisposeCore()
        {
        }

        /// <inheritdoc/>
        public override string ToString() => $"{GetType()}({Name})";
    }
}
