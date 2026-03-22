using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Xml.Linq;

namespace RedFox.Graphics3D
{
    /// <summary>
    /// Represents a node in a scene graph. Scene nodes can have a parent and zero or more
    /// child nodes, carry transform information and optional rendering handles, and
    /// participate in update traversal.
    /// </summary>
    public abstract class SceneNode : IUpdatable
    {
        private List<SceneNode>? _children = null;

        /// <summary>
        /// Gets or sets the name associated with the node.
        /// </summary>
        public string Name { get; set; }

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
        /// Gets or sets the graphics handle used for rendering operations.
        /// </summary>
        public object? GraphicsHandle { get; set; }

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
        /// Moves this node to a new parent while preserving world transforms.
        /// </summary>
        /// <param name="newParent">The new parent node, or <see langword="null"/> to detach.</param>
        public void MoveTo(SceneNode? newParent) => MoveTo(newParent, ReparentTransformMode.PreserveWorld);

        /// <summary>
        /// Moves this node to a new parent while preserving the selected transform space.
        /// </summary>
        /// <param name="newParent">The new parent node, or <see langword="null"/> to detach.</param>
        /// <param name="transformMode">The transform preservation mode used during reparenting.</param>
        public void MoveTo(SceneNode? newParent, ReparentTransformMode transformMode)
        {
            if (newParent == Parent)
                return;

            if (newParent is not null)
            {
                Parent?._children?.Remove(this);
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
        public bool IsDescendantOf(SceneNode node)
        {
            var current = Parent;

            while (current != null)
            {
                if (current == node)
                    return true;

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
        public bool IsDescendantOf(string name) => IsDescendantOf(name, StringComparison.CurrentCulture);

        /// <summary>
        /// Determines whether this node is a descendant of an ancestor with the specified name
        /// using the provided string comparison.
        /// </summary>
        /// <param name="name">The name of the ancestor to test for.</param>
        /// <param name="comparisonType">The string comparison to use when comparing names.</param>
        /// <returns><see langword="true"/> if an ancestor with the given name exists; otherwise <see langword="false"/>.</returns>
        public bool IsDescendantOf(string name, StringComparison comparisonType)
        {
            var current = Parent;

            while (current != null)
            {
                if (current.Name.Equals(name, comparisonType))
                    return true;

                current = current.Parent;
            }

            return false;
        }

        /// <summary>
        /// Enumerates all descendant nodes in depth-first order.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{SceneNode}"/> yielding descendant nodes.</returns>
        public IEnumerable<SceneNode> EnumerateDescendants()
        {
            if (_children is null)
                yield break;

            foreach (var child in _children)
            {
                yield return child;

                foreach (var descendant in child.EnumerateDescendants())
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>
        /// Enumerates ancestor nodes from the immediate parent up to the root.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{SceneNode}"/> yielding ancestor nodes.</returns>
        public IEnumerable<SceneNode> EnumerateAncestors()
        {
            var current = Parent;

            while (current != null)
            {
                yield return current;
                current = current.Parent;
            }
        }

        /// <summary>
        /// Enumerates direct child nodes of this node.
        /// </summary>
        /// <returns>An <see cref="IEnumerable{SceneNode}"/> of child nodes (empty if none).</returns>
        public IEnumerable<SceneNode> EnumerateChildren() => _children ?? Enumerable.Empty<SceneNode>();

        /// <summary>
        /// Enumerates descendant nodes of the specified type.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <returns>An <see cref="IEnumerable{T}"/> yielding matching descendant nodes.</returns>
        public IEnumerable<T> EnumerateDescendants<T>() where T : SceneNode
        {
            if (_children is null)
                yield break;

            foreach (var child in _children)
            {
                if (child.GetType() == typeof(T))
                    yield return (T)child;

                foreach (var descendant in child.EnumerateDescendants())
                {
                    if (descendant.GetType() == typeof(T))
                        yield return (T)descendant;
                }
            }
        }

        /// <summary>
        /// Enumerates ancestor nodes of the specified type.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <returns>An <see cref="IEnumerable{T}"/> yielding matching ancestor nodes.</returns>
        public IEnumerable<T> EnumerateAncestors<T>() where T : SceneNode => EnumerateAncestors().OfType<T>();

        /// <summary>
        /// Enumerates child nodes of the specified type.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <returns>An <see cref="IEnumerable{T}"/> yielding matching child nodes.</returns>
        public IEnumerable<T> EnumerateChildren<T>() where T : SceneNode => EnumerateChildren().OfType<T>();

        /// <summary>
        /// Returns an array containing all descendant nodes.
        /// </summary>
        public SceneNode[] GetDescendants() => [.. EnumerateDescendants()];

        /// <summary>
        /// Returns an array containing all ancestor nodes.
        /// </summary>
        public SceneNode[] GetAncestors() => [.. EnumerateAncestors()];

        /// <summary>
        /// Returns an array containing all descendant nodes of the specified type.
        /// </summary>
        public T[] GetDescendants<T>() where T : SceneNode => [.. EnumerateDescendants<T>()];

        /// <summary>
        /// Returns an array containing all ancestor nodes of the specified type.
        /// </summary>
        public T[] GetAncestors<T>() where T : SceneNode => [.. EnumerateAncestors<T>()];

        /// <summary>
        /// Attempts to find a direct child with the specified name using current culture comparison.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="node">The matching child node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching child was found; otherwise <c>false</c>.</returns>
        public bool TryFindChild(string name, [NotNullWhen(true)] out SceneNode? node) => TryFindChild(name, StringComparison.CurrentCulture, out node);

        /// <summary>
        /// Attempts to find a direct child with the specified name using the provided comparison.
        /// </summary>
        /// <param name="name">The name to search for.</param>
        /// <param name="comparisonType">The comparison to use.</param>
        /// <param name="node">The matching child node when found; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if a matching child was found; otherwise <c>false</c>.</returns>
        public bool TryFindChild(string name, StringComparison comparisonType, [NotNullWhen(true)] out SceneNode? node)
        {
            node = EnumerateChildren().FirstOrDefault(x => x.Name.Equals(name, comparisonType));
            return node is not null;
        }

        /// <summary>
        /// Attempts to find a descendant with the specified name using current culture comparison.
        /// </summary>
        public bool TryFindDescendant(string name, [NotNullWhen(true)] out SceneNode? node) => TryFindDescendant(name, StringComparison.CurrentCulture, out node);

        /// <summary>
        /// Attempts to find a descendant with the specified name using the provided comparison.
        /// </summary>
        public bool TryFindDescendant(string name, StringComparison comparisonType, [NotNullWhen(true)] out SceneNode? node)
        {
            node = EnumerateDescendants().FirstOrDefault(x => x.Name.Equals(name, comparisonType));
            return node is not null;
        }

        /// <summary>
        /// Attempts to find an ancestor with the specified name using current culture comparison.
        /// </summary>
        public bool TryFindAncestor(string name, [NotNullWhen(true)] out SceneNode? node) => TryFindAncestor(name, StringComparison.CurrentCulture, out node);

        /// <summary>
        /// Attempts to find an ancestor with the specified name using the provided comparison.
        /// </summary>
        public bool TryFindAncestor(string name, StringComparison comparisonType, [NotNullWhen(true)] out SceneNode? node)
        {
            node = EnumerateAncestors().FirstOrDefault(x => x.Name.Equals(name, comparisonType));
            return node is not null;
        }

        /// <summary>
        /// Attempts to find a direct child of the specified type and name using current culture comparison.
        /// </summary>
        public bool TryFindChild<T>(string name, [NotNullWhen(true)] out T? node) where T : SceneNode => TryFindChild(name, StringComparison.CurrentCulture, out node);

        /// <summary>
        /// Attempts to find a direct child of the specified type and name using the provided comparison.
        /// </summary>
        public bool TryFindChild<T>(string name, StringComparison comparisonType, [NotNullWhen(true)] out T? node) where T : SceneNode
        {
            node = EnumerateChildren<T>().FirstOrDefault(x => x.Name.Equals(name, comparisonType));
            return node is not null;
        }

        /// <summary>
        /// Attempts to find a descendant of the specified type and name using current culture comparison.
        /// </summary>
        public bool TryFindDescendant<T>(string name, [NotNullWhen(true)] out T? node) where T : SceneNode => TryFindDescendant(name, StringComparison.CurrentCulture, out node);

        /// <summary>
        /// Attempts to find a descendant of the specified type and name using the provided comparison.
        /// </summary>
        public bool TryFindDescendant<T>(string name, StringComparison comparisonType, [NotNullWhen(true)] out T? node) where T : SceneNode
        {
            node = EnumerateDescendants<T>().FirstOrDefault(x => x.Name.Equals(name, comparisonType));
            return node is not null;
        }

        /// <summary>
        /// Attempts to find an ancestor of the specified type and name using current culture comparison.
        /// </summary>
        public bool TryFindAncestor<T>(string name, [NotNullWhen(true)] out T? node) where T : SceneNode => TryFindAncestor(name, StringComparison.CurrentCulture, out node);

        private bool TryFindAncestor<T>(string name, StringComparison comparisonType, [NotNullWhen(true)] out T? node) where T : SceneNode
        {
            node = EnumerateAncestors<T>().FirstOrDefault(x => x.Name.Equals(name, comparisonType));
            return node is not null;
        }

        public SceneNode FindChild(string name) => FindChild(name, StringComparison.CurrentCulture);

        public SceneNode FindChild(string name, StringComparison comparisonType)
        {
            if (TryFindChild(name, comparisonType, out var node))
                return node;

            throw new SceneNodeNotFoundException($"A child with the name: {name} was not found in: {Name}");
        }

        public SceneNode FindDescendant(string name) => FindDescendant(name, StringComparison.CurrentCulture);

        public SceneNode FindDescendant(string name, StringComparison comparisonType)
        {
            if (TryFindDescendant(name, comparisonType, out var node))
                return node;

            throw new SceneNodeNotFoundException($"A descendant with the name: {name} was not found in: {Name}");
        }

        public SceneNode FindAncestor(string name) => FindAncestor(name, StringComparison.CurrentCulture);

        public SceneNode FindAncestor(string name, StringComparison comparisonType)
        {
            if (TryFindAncestor(name, comparisonType, out var node))
                return node;

            throw new SceneNodeNotFoundException($"An ancestor with the name: {name} was not found in: {Name}");
        }

        public T FindChild<T>(string name) where T : SceneNode => FindChild<T>(name, StringComparison.CurrentCulture);

        public T FindChild<T>(string name, StringComparison comparisonType) where T : SceneNode
        {
            if (TryFindChild<T>(name, comparisonType, out var node))
                return node;

            throw new SceneNodeNotFoundException($"A child with the name: {name} of type: {typeof(T)} was not found in: {Name}");
        }

        public T FindDescendant<T>(string name) where T : SceneNode => FindDescendant<T>(name, StringComparison.CurrentCulture);

        public T FindDescendant<T>(string name, StringComparison comparisonType) where T : SceneNode
        {
            if (TryFindDescendant<T>(name, comparisonType, out var node))
                return node;

            throw new SceneNodeNotFoundException($"A descendant with the name: {name} of type: {typeof(T)} was not found in: {Name}");
        }

        public T FindAncestor<T>(string name) where T : SceneNode => FindAncestor<T>(name, StringComparison.CurrentCulture);

        public T FindAncestor<T>(string name, StringComparison comparisonType) where T : SceneNode
        {
            if (TryFindAncestor<T>(name, comparisonType, out var node))
                return node;

            throw new SceneNodeNotFoundException($"An ancestor with the name: {name} of type: {typeof(T)} was not found in: {Name}");
        }

        /// <summary>
        /// Removes a child node by reference.
        /// </summary>
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

        public T AddNode<T>(T node) where T : SceneNode
        {
            if (node.Parent != null)
                throw new SceneNodeParentException($"Node '{node.Name}' already has a parent.");
            _children ??= [];
            if (_children.Any(x => x.Name.Equals(node.Name, StringComparison.CurrentCultureIgnoreCase)))
                throw new SceneNodeDuplicateException($"A node with the name: {node.Name} already exists in: {Name}");
            _children.Add(node);
            node.Parent = this;
            OnChildAdded(node);
            return node;
        }

        /// <summary>
        /// Adds a new node of type T as a child.
        /// </summary>
        public T AddNode<T>() where T : SceneNode, new() => AddNode(new T());

        /// <summary>
        /// Adds a new node of type T with the specified name as a child.
        /// </summary>
        public T AddNode<T>(string name) where T : SceneNode, new()
        {
            var node = AddNode(new T());
            node.Name = name;
            return node;
        }

        public void Update(float deltaTime)
        {
            if (Flags.HasFlag(SceneNodeFlags.Disabled))
                return;

            OnUpdate(deltaTime);

            _children?.ForEach(x => x.Update(deltaTime));
        }

        protected virtual void OnUpdate(float deltaTime)
        {
        }

        public T FirstOfType<T>() where T : SceneNode
        {
            if (this is T tSelf)
                return tSelf;
            var found = EnumerateDescendants<T>().FirstOrDefault();
            if (found is null)
                throw new SceneNodeNotFoundException($"No node of type {typeof(T)} found in: {Name}");
            return found;
        }

        public T? TryGetFirstOfType<T>() where T : SceneNode
        {
            if (this is T tSelf)
            {
                return tSelf;
            }

            return EnumerateDescendants<T>().FirstOrDefault();
        }

        public bool TryGetFirstOfType<T>([NotNullWhen(true)] out T? node) where T : SceneNode
        {
            if (this is T tSelf)
            {
                node = tSelf;
                return true;
            }
            node = EnumerateDescendants<T>().FirstOrDefault();
            return node is not null;
        }

        public SceneNode FindByPath(string path)
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
            if (current == null)
                throw new SceneNodeNotFoundException($"Node at path '{path}' not found.");
            return current;
        }

        public bool TryFindByPath(string path, [NotNullWhen(true)] out SceneNode? node)
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
            if (current == null)
                return false;
            node = current;
            return true;
        }

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

        public IEnumerable<SceneNode> EnumerateSiblings()
        {
            return Parent?.EnumerateChildren().Where(n => n != this) ?? [];
        }

        public SceneNode[] GetSiblings() => EnumerateSiblings().ToArray();

        public void Detach()
        {
            Parent?.RemoveNode(this);
        }

        public void Traverse(Action<SceneNode> action)
        {
            action(this);
            foreach (var child in EnumerateDescendants())
                action(child);
        }

        public SceneNode GetRoot()
        {
            var current = this;
            while (current.Parent != null)
                current = current.Parent;
            return current;
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
        public IEnumerable<SceneNode> EnumerateHierarchy()
        {
            yield return this;

            foreach (var descendant in EnumerateDescendants())
            {
                yield return descendant;
            }
        }

        /// <summary>
        /// Enumerates this node and all descendants of the specified type.
        /// </summary>
        /// <typeparam name="T">The node type to filter for.</typeparam>
        /// <returns>An <see cref="IEnumerable{T}"/> yielding matching nodes.</returns>
        public IEnumerable<T> EnumerateHierarchy<T>() where T : SceneNode => EnumerateHierarchy().OfType<T>();

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

        /// <inheritdoc/>
        public override string ToString() => Name;
    }
}
