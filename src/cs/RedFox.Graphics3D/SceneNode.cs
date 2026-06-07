using System.Diagnostics.CodeAnalysis;
using System.IO.Enumeration;
using System.Numerics;
using System.Xml.Linq;
using RedFox.Graphics3D.Rendering;
using RedFox.Graphics3D.Rendering.Materials;

namespace RedFox.Graphics3D;

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
    /// Gets the slash-delimited path of this node within its hierarchy, built from the names
    /// of this node and its ancestors up to the root.
    /// </summary>
    public string Path => Parent is null ? Name : $"{Parent.Path}/{Name}";

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
    /// Gets or Sets the arbitrary attributes associated with the node.
    /// </summary>
    public Dictionary<string, object>? Attributes { get; set; }

    /// <summary>
    /// Gets or Sets the custom user data assigned to the node.
    /// </summary>
    public object? UserData { get; set; }

    /// <summary>
    /// Gets or Sets the custom user ID assigned to the node.
    /// </summary>
    public UInt128 UserId { get; set; }

    /// <summary>
    /// Initializes a new instance of <see cref="SceneNode"/> with a generated name.
    /// </summary>
    public SceneNode() : this($"SceneNode{SceneNodeId.GetNextId()}")
    {
        // By default - a new node is "selected"
        Flags = SceneNodeFlags.Selected;
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
        // By default - a new node is "selected"
        Flags = SceneNodeFlags.Selected;
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

    private bool MatchesName(string namePattern) =>
        FileSystemName.MatchesSimpleExpression(namePattern, Name);

    /// <summary>
    /// Moves this node to a new parent while preserving world transforms.
    /// Duplicate detection is constrained to direct siblings and throws on conflict.
    /// </summary>
    /// <param name="newParent">The new parent node, or <see langword="null"/> to detach.</param>
    /// <exception cref="SceneNodeDuplicateException">Thrown if a node with the same name already exists in the new parent.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an invalid transform mode is specified.</exception>
    public void MoveTo(SceneNode? newParent) => MoveTo(newParent, ReparentTransformMode.PreserveWorld);

    /// <summary>
    /// Moves this node to a new parent while preserving the selected transform space.
    /// Duplicate detection is constrained to direct siblings and throws on conflict.
    /// </summary>
    /// <param name="newParent">The new parent node, or <see langword="null"/> to detach.</param>
    /// <param name="transformMode">The transform preservation mode used during reparenting.</param>
    /// <exception cref="SceneNodeDuplicateException">Thrown if a node with the same name already exists in the new parent.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an invalid transform mode is specified.</exception>
    public void MoveTo(SceneNode? newParent, ReparentTransformMode transformMode)
    {
        if (ReferenceEquals(newParent, Parent))
        {
            return;
        }

        MoveTo(newParent, transformMode, SceneNodeMatchScope.Siblings, SceneMergeStrategy.Throw);
    }

    /// <summary>
    /// Moves this node to a new parent while preserving world transforms, resolving any duplicate
    /// according to <paramref name="scope"/> and <paramref name="strategy"/>. The check is applied
    /// recursively to every descendant of this node so the entire moved subtree is deduplicated.
    /// </summary>
    /// <param name="newParent">The new parent node, or <see langword="null"/> to detach.</param>
    /// <param name="scope">Where duplicate nodes are searched for in the target hierarchy.</param>
    /// <param name="strategy">How a discovered duplicate is resolved.</param>
    /// <returns>The node that represents the committed result in the target hierarchy.
    /// May differ from this node when <paramref name="strategy"/> is <see cref="SceneMergeStrategy.Skip"/>
    /// or <see cref="SceneMergeStrategy.Merge"/>.</returns>
    public SceneNode MoveTo(SceneNode? newParent, SceneNodeMatchScope scope, SceneMergeStrategy strategy)
        => MoveTo(newParent, ReparentTransformMode.PreserveWorld, scope, strategy);

    /// <summary>
    /// Moves this node to a new parent while preserving the selected transform space, resolving any
    /// duplicate according to <paramref name="scope"/> and <paramref name="strategy"/>. The check is
    /// applied recursively to every descendant of this node so the entire moved subtree is
    /// deduplicated; for example, attaching a material under a scene with
    /// <see cref="SceneNodeMatchScope.WholeScene"/> also reconciles every child texture against the
    /// rest of the scene.
    /// </summary>
    /// <param name="newParent">The new parent node, or <see langword="null"/> to detach.</param>
    /// <param name="transformMode">The transform preservation mode used during reparenting.</param>
    /// <param name="scope">Where duplicate nodes are searched for in the target hierarchy.</param>
    /// <param name="strategy">How a discovered duplicate is resolved.</param>
    /// <returns>The node that represents the committed result in the target hierarchy.
    /// May differ from this node when <paramref name="strategy"/> is <see cref="SceneMergeStrategy.Skip"/>
    /// or <see cref="SceneMergeStrategy.Merge"/>.</returns>
    public SceneNode MoveTo(SceneNode? newParent, ReparentTransformMode transformMode, SceneNodeMatchScope scope, SceneMergeStrategy strategy)
    {
        if (newParent is null)
        {
            Detach();
            return this;
        }

        return AttachInto(newParent, scope, strategy, transformMode);
    }

    /// <summary>
    /// Resolves duplicates in <paramref name="targetParent"/>'s hierarchy and either attaches this
    /// node as a child of <paramref name="targetParent"/> or surrenders it according to
    /// <paramref name="strategy"/>. When the incoming subtree is committed into the target (every
    /// outcome except <see cref="SceneMergeStrategy.Skip"/> at this level) each descendant is also
    /// checked against <paramref name="scope"/> so duplicates anywhere in the incoming hierarchy are
    /// resolved by the same rules.
    /// </summary>
    private SceneNode AttachInto(SceneNode targetParent, SceneNodeMatchScope scope, SceneMergeStrategy strategy, ReparentTransformMode transformMode)
    {
        SceneNode? existing = FindDuplicateInScope(targetParent, scope);

        if (existing is null || ReferenceEquals(existing, this))
        {
            AttachAsChild(targetParent, transformMode);
            ApplyScopeToChildren(scope, strategy, transformMode);
            return this;
        }

        SceneNode targetRoot = targetParent.GetRoot();
        SceneNode stagingRoot = GetRoot();

        switch (strategy)
        {
            case SceneMergeStrategy.Throw:
                throw new SceneNodeDuplicateException($"A node with the name: {Name} already exists in: {targetParent.Name}");

            case SceneMergeStrategy.Rename:
                Name = MakeUniqueName(targetParent, scope);
                AttachAsChild(targetParent, transformMode);
                ApplyScopeToChildren(scope, strategy, transformMode);
                return this;

            case SceneMergeStrategy.Skip:
                RedirectReferences(this, existing, targetRoot, stagingRoot);
                Detach();
                return existing;

            case SceneMergeStrategy.Replace:
                RedirectReferences(existing, this, targetRoot, stagingRoot);
                existing.Detach();
                AttachAsChild(targetParent, transformMode);
                ApplyScopeToChildren(scope, strategy, transformMode);
                return this;

            case SceneMergeStrategy.Merge:
                ApplyMergeTransform(existing, this, transformMode);
                foreach (SceneNode child in EnumerateChildren().ToArray())
                {
                    child.AttachInto(existing, scope, strategy, transformMode);
                }
                RedirectReferences(this, existing, targetRoot, stagingRoot);
                Detach();
                return existing;

            default:
                throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown merge strategy.");
        }
    }

    /// <summary>
    /// After this node is committed into the target hierarchy, applies the same scope/strategy to
    /// each of its children so the entire incoming subtree is deduplicated.
    /// </summary>
    private void ApplyScopeToChildren(SceneNodeMatchScope scope, SceneMergeStrategy strategy, ReparentTransformMode transformMode)
    {
        if (scope == SceneNodeMatchScope.None)
        {
            return;
        }

        foreach (SceneNode child in EnumerateChildren().ToArray())
        {
            if (!ReferenceEquals(child.Parent, this))
            {
                continue;
            }
            child.AttachInto(this, scope, strategy, transformMode);
        }
    }

    /// <summary>
    /// Reparents this node under <paramref name="newParent"/> applying the requested transform mode.
    /// Performs no duplicate detection; callers must enforce uniqueness when required.
    /// </summary>
    private void AttachAsChild(SceneNode newParent, ReparentTransformMode transformMode)
    {
        if (ReferenceEquals(newParent, Parent))
        {
            return;
        }

        SceneNode? oldParent = Parent;
        Scene? oldScene = Scene;
        Scene? newScene = newParent.Scene;

        oldParent?._children?.Remove(this);
        if (oldParent is not null)
        {
            oldParent.OnChildRemoved(this);
        }

        Parent = newParent;
        newParent._children ??= [];
        newParent._children.Add(this);
        SetScene(newScene);
        newParent.OnChildAdded(this);

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

        foreach (SceneNode node in EnumerateDescendants())
        {
            node.BindTransform.WorldPosition = null;
            node.BindTransform.WorldRotation = null;
            node.LiveTransform.WorldPosition = null;
            node.LiveTransform.WorldRotation = null;
        }

        if (oldScene is not null && !ReferenceEquals(oldScene, newScene))
        {
            oldScene.NotifyChanged(SceneChangeKind.NodeRemoved, this);
        }

        if (newScene is not null)
        {
            newScene.NotifyChanged(SceneChangeKind.NodeAdded, this);
        }
    }

    /// <summary>
    /// Finds the first node within <paramref name="scope"/> that matches this node's name (case
    /// insensitive) and runtime type. Returns <see langword="null"/> when no duplicate is present.
    /// This node itself is always skipped.
    /// </summary>
    private SceneNode? FindDuplicateInScope(SceneNode targetParent, SceneNodeMatchScope scope)
    {
        foreach (SceneNode candidate in EnumerateScopeCandidates(targetParent, scope))
        {
            if (ReferenceEquals(candidate, this))
                continue;
            if (!candidate.Name.Equals(Name, StringComparison.CurrentCultureIgnoreCase))
                continue;
            if (candidate.GetType() != GetType())
                continue;
            return candidate;
        }

        return null;
    }

    /// <summary>
    /// Enumerates the candidate nodes implied by <paramref name="scope"/>. When the
    /// <see cref="SceneNodeMatchScope.WholeScene"/> flag is set the entire hierarchy rooted at
    /// <paramref name="targetParent"/> is yielded; otherwise the requested flag subsets are yielded
    /// in order without deduplication.
    /// </summary>
    private static IEnumerable<SceneNode> EnumerateScopeCandidates(SceneNode targetParent, SceneNodeMatchScope scope)
    {
        if (scope == SceneNodeMatchScope.None)
        {
            yield break;
        }

        if (scope.HasFlag(SceneNodeMatchScope.WholeScene))
        {
            foreach (SceneNode node in targetParent.GetRoot().EnumerateHierarchy())
            {
                yield return node;
            }
            yield break;
        }

        if (scope.HasFlag(SceneNodeMatchScope.Siblings))
        {
            foreach (SceneNode node in targetParent.EnumerateChildren())
            {
                yield return node;
            }
        }

        if (scope.HasFlag(SceneNodeMatchScope.Descendants))
        {
            foreach (SceneNode node in targetParent.EnumerateDescendants())
            {
                yield return node;
            }
        }
    }

    /// <summary>
    /// Generates a unique name based on this node's current name by appending an ascending numeric
    /// suffix until no other node in <paramref name="scope"/> shares it.
    /// </summary>
    private string MakeUniqueName(SceneNode targetParent, SceneNodeMatchScope scope)
    {
        string baseName = Name;
        int index = 1;
        string candidate;
        do
        {
            candidate = $"{baseName}_{index++}";
        }
        while (HasNameInScope(targetParent, candidate, scope));

        return candidate;
    }

    private bool HasNameInScope(SceneNode targetParent, string name, SceneNodeMatchScope scope)
    {
        foreach (SceneNode candidate in EnumerateScopeCandidates(targetParent, scope))
        {
            if (ReferenceEquals(candidate, this))
                continue;
            if (candidate.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Walks both the target hierarchy and the staging hierarchy invoking <see cref="Swap"/> so that
    /// derived nodes can redirect any references they hold from <paramref name="oldNode"/> to
    /// <paramref name="newNode"/>.
    /// </summary>
    private static void RedirectReferences(SceneNode oldNode, SceneNode newNode, SceneNode targetRoot, SceneNode stagingRoot)
    {
        foreach (SceneNode node in targetRoot.EnumerateHierarchy())
        {
            node.Swap(oldNode, newNode);
        }

        if (!ReferenceEquals(stagingRoot, targetRoot))
        {
            foreach (SceneNode node in stagingRoot.EnumerateHierarchy())
            {
                node.Swap(oldNode, newNode);
            }
        }
    }

    /// <summary>
    /// Copies transforms from <paramref name="incoming"/> onto <paramref name="existing"/> when
    /// resolving a <see cref="SceneMergeStrategy.Merge"/>. The semantics mirror
    /// <see cref="ReparentTransformMode"/> for reparenting.
    /// </summary>
    private static void ApplyMergeTransform(SceneNode existing, SceneNode incoming, ReparentTransformMode mode)
    {
        switch (mode)
        {
            case ReparentTransformMode.PreserveExisting:
                break;

            case ReparentTransformMode.PreserveLocal:
                existing.BindTransform.LocalPosition = incoming.BindTransform.LocalPosition;
                existing.BindTransform.LocalRotation = incoming.BindTransform.LocalRotation;
                existing.BindTransform.Scale = incoming.BindTransform.Scale;
                existing.LiveTransform.LocalPosition = incoming.LiveTransform.LocalPosition;
                existing.LiveTransform.LocalRotation = incoming.LiveTransform.LocalRotation;
                existing.LiveTransform.Scale = incoming.LiveTransform.Scale;
                break;

            case ReparentTransformMode.PreserveWorld:
                existing.BindTransform.WorldPosition = incoming.BindTransform.WorldPosition;
                existing.BindTransform.WorldRotation = incoming.BindTransform.WorldRotation;
                existing.BindTransform.Scale = incoming.BindTransform.Scale;
                existing.LiveTransform.WorldPosition = incoming.LiveTransform.WorldPosition;
                existing.LiveTransform.WorldRotation = incoming.LiveTransform.WorldRotation;
                existing.LiveTransform.Scale = incoming.LiveTransform.Scale;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown reparent transform mode.");
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
        _children?.Where(x => x.MatchesFilter(filter)) ?? [];

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
    /// Enumerates all descendant nodes in depth-first order and performs the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each descendant node.</param>
    public void EnumerateDescendants(Action<SceneNode> action)
    {
        foreach (var descendant in EnumerateDescendants())
            action(descendant);
    }

    /// <summary>
    /// Enumerates all descendant nodes in depth-first order that match the provided filter and performs the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each descendant node.</param>
    /// <param name="filter">The flags descendant nodes must contain to be visited.</param>
    public void EnumerateDescendants(Action<SceneNode> action, SceneNodeFlags filter)
    {
        foreach (var descendant in EnumerateDescendants(filter))
            action(descendant);
    }

    /// <summary>
    /// Enumerates all descendant nodes of the specified type in depth-first order and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="action">The action to perform on each matching descendant node.</param>
    public void EnumerateDescendants<T>(Action<T> action) where T : SceneNode
    {
        foreach (var descendant in EnumerateDescendants<T>())
            action(descendant);
    }

    /// <summary>
    /// Enumerates all descendant nodes of the specified type in depth-first order that match the provided filter and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="action">The action to perform on each matching descendant node.</param>
    /// <param name="filter">The flags descendant nodes must contain to be visited.</param>
    public void EnumerateDescendants<T>(Action<T> action, SceneNodeFlags filter) where T : SceneNode
    {
        foreach (var descendant in EnumerateDescendants<T>(filter))
            action(descendant);
    }

    /// <summary>
    /// Enumerates all descendant nodes that match the specified predicate in depth-first order and performs the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate descendant nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching descendant node.</param>
    public void EnumerateDescendants(Func<SceneNode, bool> predicate, Action<SceneNode> action)
    {
        foreach (var descendant in EnumerateDescendants().Where(predicate))
            action(descendant);
    }

    /// <summary>
    /// Enumerates all descendant nodes that match the specified predicate and flags in depth-first order and performs the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate descendant nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching descendant node.</param>
    /// <param name="filter">The flags descendant nodes must contain to be visited.</param>
    public void EnumerateDescendants(Func<SceneNode, bool> predicate, Action<SceneNode> action, SceneNodeFlags filter)
    {
        foreach (var descendant in EnumerateDescendants(filter).Where(predicate))
            action(descendant);
    }

    /// <summary>
    /// Enumerates all descendant nodes of the specified type that match the specified predicate and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="predicate">The predicate descendant nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching descendant node.</param>
    public void EnumerateDescendants<T>(Func<T, bool> predicate, Action<T> action) where T : SceneNode
    {
        foreach (var descendant in EnumerateDescendants<T>().Where(predicate))
            action(descendant);
    }

    /// <summary>
    /// Enumerates all descendant nodes of the specified type that match the specified predicate and flags and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="predicate">The predicate descendant nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching descendant node.</param>
    /// <param name="filter">The flags descendant nodes must contain to be visited.</param>
    public void EnumerateDescendants<T>(Func<T, bool> predicate, Action<T> action, SceneNodeFlags filter) where T : SceneNode
    {
        foreach (var descendant in EnumerateDescendants<T>(filter).Where(predicate))
            action(descendant);
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
    /// Enumerates all ancestor nodes from the immediate parent up to the root and performs the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each ancestor node.</param>
    public void EnumerateAncestors(Action<SceneNode> action)
    {
        foreach (var ancestor in EnumerateAncestors())
            action(ancestor);
    }

    /// <summary>
    /// Enumerates all ancestor nodes from the immediate parent up to the root that match the provided filter and performs the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each ancestor node.</param>
    /// <param name="filter">The flags ancestor nodes must contain to be visited.</param>
    public void EnumerateAncestors(Action<SceneNode> action, SceneNodeFlags filter)
    {
        foreach (var ancestor in EnumerateAncestors(filter))
            action(ancestor);
    }

    /// <summary>
    /// Enumerates all ancestor nodes of the specified type and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="action">The action to perform on each matching ancestor node.</param>
    public void EnumerateAncestors<T>(Action<T> action) where T : SceneNode
    {
        foreach (var ancestor in EnumerateAncestors<T>())
            action(ancestor);
    }

    /// <summary>
    /// Enumerates all ancestor nodes of the specified type that match the provided filter and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="action">The action to perform on each matching ancestor node.</param>
    /// <param name="filter">The flags ancestor nodes must contain to be visited.</param>
    public void EnumerateAncestors<T>(Action<T> action, SceneNodeFlags filter) where T : SceneNode
    {
        foreach (var ancestor in EnumerateAncestors<T>(filter))
            action(ancestor);
    }

    /// <summary>
    /// Enumerates all ancestor nodes that match the specified predicate and performs the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate ancestor nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching ancestor node.</param>
    public void EnumerateAncestors(Func<SceneNode, bool> predicate, Action<SceneNode> action)
    {
        foreach (var ancestor in EnumerateAncestors().Where(predicate))
            action(ancestor);
    }

    /// <summary>
    /// Enumerates all ancestor nodes that match the specified predicate and flags and performs the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate ancestor nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching ancestor node.</param>
    /// <param name="filter">The flags ancestor nodes must contain to be visited.</param>
    public void EnumerateAncestors(Func<SceneNode, bool> predicate, Action<SceneNode> action, SceneNodeFlags filter)
    {
        foreach (var ancestor in EnumerateAncestors(filter).Where(predicate))
            action(ancestor);
    }

    /// <summary>
    /// Enumerates all ancestor nodes of the specified type that match the specified predicate and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="predicate">The predicate ancestor nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching ancestor node.</param>
    public void EnumerateAncestors<T>(Func<T, bool> predicate, Action<T> action) where T : SceneNode
    {
        foreach (var ancestor in EnumerateAncestors<T>().Where(predicate))
            action(ancestor);
    }

    /// <summary>
    /// Enumerates all ancestor nodes of the specified type that match the specified predicate and flags and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="predicate">The predicate ancestor nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching ancestor node.</param>
    /// <param name="filter">The flags ancestor nodes must contain to be visited.</param>
    public void EnumerateAncestors<T>(Func<T, bool> predicate, Action<T> action, SceneNodeFlags filter) where T : SceneNode
    {
        foreach (var ancestor in EnumerateAncestors<T>(filter).Where(predicate))
            action(ancestor);
    }

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
    /// Enumerates all child nodes and performs the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each child node.</param>
    public void EnumerateChildren(Action<SceneNode> action)
    {
        foreach (var child in EnumerateChildren())
            action(child);
    }

    /// <summary>
    /// Enumerates all child nodes that match the provided filter and performs the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each child node.</param>
    /// <param name="filter">The flags child nodes must contain to be visited.</param>
    public void EnumerateChildren(Action<SceneNode> action, SceneNodeFlags filter)
    {
        foreach (var child in EnumerateChildren(filter))
            action(child);
    }

    /// <summary>
    /// Enumerates all child nodes of the specified type and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="action">The action to perform on each matching child node.</param>
    public void EnumerateChildren<T>(Action<T> action) where T : SceneNode
    {
        foreach (var child in EnumerateChildren<T>())
            action(child);
    }

    /// <summary>
    /// Enumerates all child nodes of the specified type that match the provided filter and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="action">The action to perform on each matching child node.</param>
    /// <param name="filter">The flags child nodes must contain to be visited.</param>
    public void EnumerateChildren<T>(Action<T> action, SceneNodeFlags filter) where T : SceneNode
    {
        foreach (var child in EnumerateChildren<T>(filter))
            action(child);
    }

    /// <summary>
    /// Enumerates all child nodes that match the specified predicate and performs the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate child nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching child node.</param>
    public void EnumerateChildren(Func<SceneNode, bool> predicate, Action<SceneNode> action)
    {
        foreach (var child in EnumerateChildren().Where(predicate))
            action(child);
    }

    /// <summary>
    /// Enumerates all child nodes that match the specified predicate and flags and performs the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate child nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching child node.</param>
    /// <param name="filter">The flags child nodes must contain to be visited.</param>
    public void EnumerateChildren(Func<SceneNode, bool> predicate, Action<SceneNode> action, SceneNodeFlags filter)
    {
        foreach (var child in EnumerateChildren(filter).Where(predicate))
            action(child);
    }

    /// <summary>
    /// Enumerates all child nodes of the specified type that match the specified predicate and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="predicate">The predicate child nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching child node.</param>
    public void EnumerateChildren<T>(Func<T, bool> predicate, Action<T> action) where T : SceneNode
    {
        foreach (var child in EnumerateChildren<T>().Where(predicate))
            action(child);
    }

    /// <summary>
    /// Enumerates all child nodes of the specified type that match the specified predicate and flags and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="predicate">The predicate child nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching child node.</param>
    /// <param name="filter">The flags child nodes must contain to be visited.</param>
    public void EnumerateChildren<T>(Func<T, bool> predicate, Action<T> action, SceneNodeFlags filter) where T : SceneNode
    {
        foreach (var child in EnumerateChildren<T>(filter).Where(predicate))
            action(child);
    }

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
        {
            return false;
        }

        Scene? scene = node.Scene ?? _scene;
        node.Parent = null;
        node.SetScene(null);
        OnChildRemoved(node);
        scene?.NotifyChanged(SceneChangeKind.NodeRemoved, node);
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
        {
            return;
        }

        Scene? scene = _scene;
        foreach (var child in _children)
        {
            child.Parent = null;
            child.SetScene(null);
            OnChildRemoved(child);
            scene?.NotifyChanged(SceneChangeKind.NodeRemoved, child);
        }

        _children.Clear();
        scene?.NotifyChanged(SceneChangeKind.Cleared, this);
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
        node.SetScene(_scene);

        OnChildAdded(node);
        _scene?.NotifyChanged(SceneChangeKind.NodeAdded, node);
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
    /// Adds the specified node as a child of this scene node and assigns the given user data.
    /// </summary>
    /// <typeparam name="T">The type of the scene node to add. Must derive from SceneNode.</typeparam>
    /// <param name="node">The node to add as a child.</param>
    /// <param name="userData">The custom user data to assign to the node.</param>
    /// <returns>The node that was added as a child.</returns>
    /// <exception cref="SceneNodeParentException">Thrown if the specified node already has a parent.</exception>
    /// <exception cref="SceneNodeDuplicateException">Thrown if a child node with the same name already exists in this node.</exception>
    public T AddNode<T>(T node, object? userData) where T : SceneNode
    {
        var added = AddNode(node);
        added.UserData = userData;
        return added;
    }

    /// <summary>
    /// Adds a new node of type T as a child and assigns the given user data.
    /// </summary>
    /// <typeparam name="T">The type of the scene node to add. Must have a parameterless constructor.</typeparam>
    /// <param name="userData">The custom user data to assign to the new node.</param>
    /// <returns>The newly created and added node.</returns>
    public T AddNode<T>(object? userData) where T : SceneNode, new()
    {
        var node = AddNode(new T());
        node.UserData = userData;
        return node;
    }

    /// <summary>
    /// Adds a new node of type T with the specified name as a child and assigns the given user data.
    /// </summary>
    /// <typeparam name="T">The type of the scene node to add. Must have a parameterless constructor.</typeparam>
    /// <param name="name">The name to assign to the new node.</param>
    /// <param name="userData">The custom user data to assign to the new node.</param>
    /// <returns>The newly created and added node.</returns>
    public T AddNode<T>(string name, object? userData) where T : SceneNode, new()
    {
        var node = AddNode(new T());
        node.Name = name;
        node.UserData = userData;
        return node;
    }

    /// <summary>
    /// Adds <paramref name="node"/> as a child of this node, resolving any duplicate found in
    /// <paramref name="scope"/> according to <paramref name="strategy"/>. World transforms are
    /// preserved across the reparent. The check is applied recursively to every descendant of
    /// <paramref name="node"/> so the entire incoming subtree is deduplicated.
    /// </summary>
    /// <typeparam name="T">The type of the scene node to add.</typeparam>
    /// <param name="node">The node to add. The node is detached from any current parent before being attached.</param>
    /// <param name="scope">Where duplicate nodes are searched for in this hierarchy.</param>
    /// <param name="strategy">How a discovered duplicate is resolved.</param>
    /// <returns>The node that represents the committed result. May differ from <paramref name="node"/>
    /// when <paramref name="strategy"/> is <see cref="SceneMergeStrategy.Skip"/> or
    /// <see cref="SceneMergeStrategy.Merge"/>.</returns>
    public T AddNode<T>(T node, SceneNodeMatchScope scope, SceneMergeStrategy strategy) where T : SceneNode
        => AddNode(node, scope, strategy, ReparentTransformMode.PreserveWorld);

    /// <summary>
    /// Adds <paramref name="node"/> as a child of this node, resolving any duplicate found in
    /// <paramref name="scope"/> according to <paramref name="strategy"/>. The check is applied
    /// recursively to every descendant of <paramref name="node"/> so the entire incoming subtree is
    /// deduplicated; for example, adding a material with
    /// <see cref="SceneNodeMatchScope.WholeScene"/> also reconciles every child texture against the
    /// rest of the scene.
    /// </summary>
    /// <typeparam name="T">The type of the scene node to add.</typeparam>
    /// <param name="node">The node to add. The node is detached from any current parent before being attached.</param>
    /// <param name="scope">Where duplicate nodes are searched for in this hierarchy.</param>
    /// <param name="strategy">How a discovered duplicate is resolved.</param>
    /// <param name="transformMode">The transform preservation mode used during reparenting and merging.</param>
    /// <returns>The node that represents the committed result. May differ from <paramref name="node"/>
    /// when <paramref name="strategy"/> is <see cref="SceneMergeStrategy.Skip"/> or
    /// <see cref="SceneMergeStrategy.Merge"/>.</returns>
    public T AddNode<T>(T node, SceneNodeMatchScope scope, SceneMergeStrategy strategy, ReparentTransformMode transformMode) where T : SceneNode
    {
        ArgumentNullException.ThrowIfNull(node);
        return (T)node.AttachInto(this, scope, strategy, transformMode);
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
    /// Enumerates all sibling nodes and performs the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each sibling node.</param>
    public void EnumerateSiblings(Action<SceneNode> action)
    {
        foreach (var sibling in EnumerateSiblings())
            action(sibling);
    }

    /// <summary>
    /// Enumerates all sibling nodes that match the provided filter and performs the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each sibling node.</param>
    /// <param name="filter">The flags sibling nodes must contain to be visited.</param>
    public void EnumerateSiblings(Action<SceneNode> action, SceneNodeFlags filter)
    {
        foreach (var sibling in EnumerateSiblings(filter))
            action(sibling);
    }

    /// <summary>
    /// Enumerates all sibling nodes that match the specified predicate and performs the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate sibling nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching sibling node.</param>
    public void EnumerateSiblings(Func<SceneNode, bool> predicate, Action<SceneNode> action)
    {
        foreach (var sibling in EnumerateSiblings().Where(predicate))
            action(sibling);
    }

    /// <summary>
    /// Enumerates all sibling nodes that match the specified predicate and flags and performs the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate sibling nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching sibling node.</param>
    /// <param name="filter">The flags sibling nodes must contain to be visited.</param>
    public void EnumerateSiblings(Func<SceneNode, bool> predicate, Action<SceneNode> action, SceneNodeFlags filter)
    {
        foreach (var sibling in EnumerateSiblings(filter).Where(predicate))
            action(sibling);
    }

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
    /// Redirects references this node holds from <paramref name="oldNode"/> to <paramref name="newNode"/>.
    /// Called during scene merges so that references such as skin bindings, material connections,
    /// and constraint targets are kept valid when a duplicate node is resolved. The base implementation
    /// does nothing; derived types override this to update their own references.
    /// </summary>
    /// <param name="oldNode">The node being replaced.</param>
    /// <param name="newNode">The node that replaces <paramref name="oldNode"/>.</param>
    public virtual void Swap(SceneNode oldNode, SceneNode newNode)
    {
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
            BindTransform.LocalRotation = Quaternion.Conjugate(Parent.GetBindWorldRotation()) * GetBindWorldRotation();
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
    /// Enumerates this node and all descendants in depth-first order and performs the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each node.</param>
    public void EnumerateHierarchy(Action<SceneNode> action)
    {
        foreach (var node in EnumerateHierarchy())
            action(node);
    }

    /// <summary>
    /// Enumerates this node and all descendants in depth-first order that match the provided filter and performs the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each node.</param>
    /// <param name="filter">The flags nodes must contain to be visited.</param>
    public void EnumerateHierarchy(Action<SceneNode> action, SceneNodeFlags filter)
    {
        foreach (var node in EnumerateHierarchy(filter))
            action(node);
    }

    /// <summary>
    /// Enumerates this node and all descendants of the specified type and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="action">The action to perform on each matching node.</param>
    public void EnumerateHierarchy<T>(Action<T> action) where T : SceneNode
    {
        foreach (var node in EnumerateHierarchy<T>())
            action(node);
    }

    /// <summary>
    /// Enumerates this node and all descendants of the specified type that match the provided filter and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="action">The action to perform on each matching node.</param>
    /// <param name="filter">The flags nodes must contain to be visited.</param>
    public void EnumerateHierarchy<T>(Action<T> action, SceneNodeFlags filter) where T : SceneNode
    {
        foreach (var node in EnumerateHierarchy<T>(filter))
            action(node);
    }

    /// <summary>
    /// Enumerates this node and all descendants that match the specified predicate and performs the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching node.</param>
    public void EnumerateHierarchy(Func<SceneNode, bool> predicate, Action<SceneNode> action)
    {
        foreach (var node in EnumerateHierarchy().Where(predicate))
            action(node);
    }

    /// <summary>
    /// Enumerates this node and all descendants that match the specified predicate and flags and performs the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching node.</param>
    /// <param name="filter">The flags nodes must contain to be visited.</param>
    public void EnumerateHierarchy(Func<SceneNode, bool> predicate, Action<SceneNode> action, SceneNodeFlags filter)
    {
        foreach (var node in EnumerateHierarchy(filter).Where(predicate))
            action(node);
    }

    /// <summary>
    /// Enumerates this node and all descendants of the specified type that match the specified predicate and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="predicate">The predicate nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching node.</param>
    public void EnumerateHierarchy<T>(Func<T, bool> predicate, Action<T> action) where T : SceneNode
    {
        foreach (var node in EnumerateHierarchy<T>().Where(predicate))
            action(node);
    }

    /// <summary>
    /// Enumerates this node and all descendants of the specified type that match the specified predicate and flags and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="predicate">The predicate nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching node.</param>
    /// <param name="filter">The flags nodes must contain to be visited.</param>
    public void EnumerateHierarchy<T>(Func<T, bool> predicate, Action<T> action, SceneNodeFlags filter) where T : SceneNode
    {
        foreach (var node in EnumerateHierarchy<T>(filter).Where(predicate))
            action(node);
    }

    /// <summary>
    /// Enumerates direct child nodes whose name matches the provided wildcard pattern.
    /// </summary>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that child names must match.</param>
    /// <returns>An <see cref="IEnumerable{SceneNode}"/> of matching child nodes.</returns>
    public IEnumerable<SceneNode> EnumerateChildren(string namePattern) =>
        EnumerateChildren(namePattern, SceneNodeFlags.None);

    /// <summary>
    /// Enumerates direct child nodes whose name matches the provided wildcard pattern and that match the provided filter.
    /// </summary>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that child names must match.</param>
    /// <param name="filter">The flags child nodes must contain to be returned.</param>
    /// <returns>An <see cref="IEnumerable{SceneNode}"/> of matching child nodes.</returns>
    public IEnumerable<SceneNode> EnumerateChildren(string namePattern, SceneNodeFlags filter) =>
        EnumerateChildren(filter).Where(x => x.MatchesName(namePattern));

    /// <summary>
    /// Enumerates child nodes whose name matches the provided wildcard pattern and performs the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each matching child node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that child names must match.</param>
    public void EnumerateChildren(Action<SceneNode> action, string namePattern)
    {
        foreach (var child in EnumerateChildren(namePattern))
            action(child);
    }

    /// <summary>
    /// Enumerates child nodes whose name matches the provided wildcard pattern and that match the provided filter, performing the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each matching child node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that child names must match.</param>
    /// <param name="filter">The flags child nodes must contain to be visited.</param>
    public void EnumerateChildren(Action<SceneNode> action, string namePattern, SceneNodeFlags filter)
    {
        foreach (var child in EnumerateChildren(namePattern, filter))
            action(child);
    }

    /// <summary>
    /// Enumerates child nodes that match the specified predicate and whose name matches the provided wildcard pattern, performing the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate child nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching child node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that child names must match.</param>
    public void EnumerateChildren(Func<SceneNode, bool> predicate, Action<SceneNode> action, string namePattern)
    {
        foreach (var child in EnumerateChildren(namePattern).Where(predicate))
            action(child);
    }

    /// <summary>
    /// Enumerates child nodes that match the specified predicate and whose name matches the provided wildcard pattern and flags, performing the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate child nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching child node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that child names must match.</param>
    /// <param name="filter">The flags child nodes must contain to be visited.</param>
    public void EnumerateChildren(Func<SceneNode, bool> predicate, Action<SceneNode> action, string namePattern, SceneNodeFlags filter)
    {
        foreach (var child in EnumerateChildren(namePattern, filter).Where(predicate))
            action(child);
    }

    /// <summary>
    /// Enumerates descendant nodes whose name matches the provided wildcard pattern.
    /// </summary>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that descendant names must match.</param>
    /// <returns>An <see cref="IEnumerable{SceneNode}"/> of matching descendant nodes.</returns>
    public IEnumerable<SceneNode> EnumerateDescendants(string namePattern) =>
        EnumerateDescendants(namePattern, SceneNodeFlags.None);

    /// <summary>
    /// Enumerates descendant nodes whose name matches the provided wildcard pattern and that match the provided filter.
    /// </summary>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that descendant names must match.</param>
    /// <param name="filter">The flags descendant nodes must contain to be returned.</param>
    /// <returns>An <see cref="IEnumerable{SceneNode}"/> of matching descendant nodes.</returns>
    public IEnumerable<SceneNode> EnumerateDescendants(string namePattern, SceneNodeFlags filter) =>
        EnumerateDescendants(filter).Where(x => x.MatchesName(namePattern));

    /// <summary>
    /// Enumerates descendant nodes whose name matches the provided wildcard pattern and performs the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each matching descendant node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that descendant names must match.</param>
    public void EnumerateDescendants(Action<SceneNode> action, string namePattern)
    {
        foreach (var descendant in EnumerateDescendants(namePattern))
            action(descendant);
    }

    /// <summary>
    /// Enumerates descendant nodes whose name matches the provided wildcard pattern and that match the provided filter, performing the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each matching descendant node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that descendant names must match.</param>
    /// <param name="filter">The flags descendant nodes must contain to be visited.</param>
    public void EnumerateDescendants(Action<SceneNode> action, string namePattern, SceneNodeFlags filter)
    {
        foreach (var descendant in EnumerateDescendants(namePattern, filter))
            action(descendant);
    }

    /// <summary>
    /// Enumerates descendant nodes that match the specified predicate and whose name matches the provided wildcard pattern, performing the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate descendant nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching descendant node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that descendant names must match.</param>
    public void EnumerateDescendants(Func<SceneNode, bool> predicate, Action<SceneNode> action, string namePattern)
    {
        foreach (var descendant in EnumerateDescendants(namePattern).Where(predicate))
            action(descendant);
    }

    /// <summary>
    /// Enumerates descendant nodes that match the specified predicate and whose name matches the provided wildcard pattern and flags, performing the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate descendant nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching descendant node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that descendant names must match.</param>
    /// <param name="filter">The flags descendant nodes must contain to be visited.</param>
    public void EnumerateDescendants(Func<SceneNode, bool> predicate, Action<SceneNode> action, string namePattern, SceneNodeFlags filter)
    {
        foreach (var descendant in EnumerateDescendants(namePattern, filter).Where(predicate))
            action(descendant);
    }

    /// <summary>
    /// Enumerates ancestor nodes whose name matches the provided wildcard pattern.
    /// </summary>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that ancestor names must match.</param>
    /// <returns>An <see cref="IEnumerable{SceneNode}"/> of matching ancestor nodes.</returns>
    public IEnumerable<SceneNode> EnumerateAncestors(string namePattern) =>
        EnumerateAncestors(namePattern, SceneNodeFlags.None);

    /// <summary>
    /// Enumerates ancestor nodes whose name matches the provided wildcard pattern and that match the provided filter.
    /// </summary>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that ancestor names must match.</param>
    /// <param name="filter">The flags ancestor nodes must contain to be returned.</param>
    /// <returns>An <see cref="IEnumerable{SceneNode}"/> of matching ancestor nodes.</returns>
    public IEnumerable<SceneNode> EnumerateAncestors(string namePattern, SceneNodeFlags filter) =>
        EnumerateAncestors(filter).Where(x => x.MatchesName(namePattern));

    /// <summary>
    /// Enumerates ancestor nodes whose name matches the provided wildcard pattern and performs the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each matching ancestor node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that ancestor names must match.</param>
    public void EnumerateAncestors(Action<SceneNode> action, string namePattern)
    {
        foreach (var ancestor in EnumerateAncestors(namePattern))
            action(ancestor);
    }

    /// <summary>
    /// Enumerates ancestor nodes whose name matches the provided wildcard pattern and that match the provided filter, performing the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each matching ancestor node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that ancestor names must match.</param>
    /// <param name="filter">The flags ancestor nodes must contain to be visited.</param>
    public void EnumerateAncestors(Action<SceneNode> action, string namePattern, SceneNodeFlags filter)
    {
        foreach (var ancestor in EnumerateAncestors(namePattern, filter))
            action(ancestor);
    }

    /// <summary>
    /// Enumerates ancestor nodes that match the specified predicate and whose name matches the provided wildcard pattern, performing the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate ancestor nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching ancestor node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that ancestor names must match.</param>
    public void EnumerateAncestors(Func<SceneNode, bool> predicate, Action<SceneNode> action, string namePattern)
    {
        foreach (var ancestor in EnumerateAncestors(namePattern).Where(predicate))
            action(ancestor);
    }

    /// <summary>
    /// Enumerates ancestor nodes that match the specified predicate and whose name matches the provided wildcard pattern and flags, performing the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate ancestor nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching ancestor node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that ancestor names must match.</param>
    /// <param name="filter">The flags ancestor nodes must contain to be visited.</param>
    public void EnumerateAncestors(Func<SceneNode, bool> predicate, Action<SceneNode> action, string namePattern, SceneNodeFlags filter)
    {
        foreach (var ancestor in EnumerateAncestors(namePattern, filter).Where(predicate))
            action(ancestor);
    }

    /// <summary>
    /// Enumerates sibling nodes whose name matches the provided wildcard pattern.
    /// </summary>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that sibling names must match.</param>
    /// <returns>An <see cref="IEnumerable{SceneNode}"/> of matching sibling nodes.</returns>
    public IEnumerable<SceneNode> EnumerateSiblings(string namePattern) =>
        EnumerateSiblings(namePattern, SceneNodeFlags.None);

    /// <summary>
    /// Enumerates sibling nodes whose name matches the provided wildcard pattern and that match the provided filter.
    /// </summary>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that sibling names must match.</param>
    /// <param name="filter">The flags sibling nodes must contain to be returned.</param>
    /// <returns>An <see cref="IEnumerable{SceneNode}"/> of matching sibling nodes.</returns>
    public IEnumerable<SceneNode> EnumerateSiblings(string namePattern, SceneNodeFlags filter) =>
        EnumerateSiblings(filter).Where(x => x.MatchesName(namePattern));

    /// <summary>
    /// Enumerates sibling nodes whose name matches the provided wildcard pattern and performs the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each matching sibling node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that sibling names must match.</param>
    public void EnumerateSiblings(Action<SceneNode> action, string namePattern)
    {
        foreach (var sibling in EnumerateSiblings(namePattern))
            action(sibling);
    }

    /// <summary>
    /// Enumerates sibling nodes whose name matches the provided wildcard pattern and that match the provided filter, performing the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each matching sibling node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that sibling names must match.</param>
    /// <param name="filter">The flags sibling nodes must contain to be visited.</param>
    public void EnumerateSiblings(Action<SceneNode> action, string namePattern, SceneNodeFlags filter)
    {
        foreach (var sibling in EnumerateSiblings(namePattern, filter))
            action(sibling);
    }

    /// <summary>
    /// Enumerates sibling nodes that match the specified predicate and whose name matches the provided wildcard pattern, performing the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate sibling nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching sibling node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that sibling names must match.</param>
    public void EnumerateSiblings(Func<SceneNode, bool> predicate, Action<SceneNode> action, string namePattern)
    {
        foreach (var sibling in EnumerateSiblings(namePattern).Where(predicate))
            action(sibling);
    }

    /// <summary>
    /// Enumerates sibling nodes that match the specified predicate and whose name matches the provided wildcard pattern and flags, performing the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate sibling nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching sibling node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that sibling names must match.</param>
    /// <param name="filter">The flags sibling nodes must contain to be visited.</param>
    public void EnumerateSiblings(Func<SceneNode, bool> predicate, Action<SceneNode> action, string namePattern, SceneNodeFlags filter)
    {
        foreach (var sibling in EnumerateSiblings(namePattern, filter).Where(predicate))
            action(sibling);
    }

    /// <summary>
    /// Enumerates this node followed by all its descendants whose name matches the provided wildcard pattern.
    /// </summary>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that node names must match.</param>
    /// <returns>An <see cref="IEnumerable{SceneNode}"/> of matching nodes.</returns>
    public IEnumerable<SceneNode> EnumerateHierarchy(string namePattern) =>
        EnumerateHierarchy(namePattern, SceneNodeFlags.None);

    /// <summary>
    /// Enumerates this node followed by all its descendants whose name matches the provided wildcard pattern and that match the provided filter.
    /// </summary>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that node names must match.</param>
    /// <param name="filter">The flags nodes must contain to be returned.</param>
    /// <returns>An <see cref="IEnumerable{SceneNode}"/> of matching nodes.</returns>
    public IEnumerable<SceneNode> EnumerateHierarchy(string namePattern, SceneNodeFlags filter) =>
        EnumerateHierarchy(filter).Where(x => x.MatchesName(namePattern));

    /// <summary>
    /// Enumerates this node and descendants whose name matches the provided wildcard pattern and performs the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each matching node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that node names must match.</param>
    public void EnumerateHierarchy(Action<SceneNode> action, string namePattern)
    {
        foreach (var node in EnumerateHierarchy(namePattern))
            action(node);
    }

    /// <summary>
    /// Enumerates this node and descendants whose name matches the provided wildcard pattern and that match the provided filter, performing the specified action on each.
    /// </summary>
    /// <param name="action">The action to perform on each matching node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that node names must match.</param>
    /// <param name="filter">The flags nodes must contain to be visited.</param>
    public void EnumerateHierarchy(Action<SceneNode> action, string namePattern, SceneNodeFlags filter)
    {
        foreach (var node in EnumerateHierarchy(namePattern, filter))
            action(node);
    }

    /// <summary>
    /// Enumerates this node and descendants that match the specified predicate and whose name matches the provided wildcard pattern, performing the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that node names must match.</param>
    public void EnumerateHierarchy(Func<SceneNode, bool> predicate, Action<SceneNode> action, string namePattern)
    {
        foreach (var node in EnumerateHierarchy(namePattern).Where(predicate))
            action(node);
    }

    /// <summary>
    /// Enumerates this node and descendants that match the specified predicate and whose name matches the provided wildcard pattern and flags, performing the specified action on each.
    /// </summary>
    /// <param name="predicate">The predicate nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that node names must match.</param>
    /// <param name="filter">The flags nodes must contain to be visited.</param>
    public void EnumerateHierarchy(Func<SceneNode, bool> predicate, Action<SceneNode> action, string namePattern, SceneNodeFlags filter)
    {
        foreach (var node in EnumerateHierarchy(namePattern, filter).Where(predicate))
            action(node);
    }

    /// <summary>
    /// Enumerates child nodes of the specified type whose name matches the provided wildcard pattern.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that child names must match.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of matching child nodes.</returns>
    public IEnumerable<T> EnumerateChildren<T>(string namePattern) where T : SceneNode =>
        EnumerateChildren<T>(namePattern, SceneNodeFlags.None);

    /// <summary>
    /// Enumerates child nodes of the specified type whose name matches the provided wildcard pattern and that match the provided filter.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that child names must match.</param>
    /// <param name="filter">The flags child nodes must contain to be returned.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of matching child nodes.</returns>
    public IEnumerable<T> EnumerateChildren<T>(string namePattern, SceneNodeFlags filter) where T : SceneNode =>
        EnumerateChildren<T>(filter).Where(x => x.MatchesName(namePattern));

    /// <summary>
    /// Enumerates child nodes of the specified type whose name matches the provided wildcard pattern and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="action">The action to perform on each matching child node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that child names must match.</param>
    public void EnumerateChildren<T>(Action<T> action, string namePattern) where T : SceneNode
    {
        foreach (var child in EnumerateChildren<T>(namePattern))
            action(child);
    }

    /// <summary>
    /// Enumerates child nodes of the specified type whose name matches the provided wildcard pattern and that match the provided filter, performing the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="action">The action to perform on each matching child node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that child names must match.</param>
    /// <param name="filter">The flags child nodes must contain to be visited.</param>
    public void EnumerateChildren<T>(Action<T> action, string namePattern, SceneNodeFlags filter) where T : SceneNode
    {
        foreach (var child in EnumerateChildren<T>(namePattern, filter))
            action(child);
    }

    /// <summary>
    /// Enumerates child nodes of the specified type that match the specified predicate and whose name matches the provided wildcard pattern, performing the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="predicate">The predicate child nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching child node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that child names must match.</param>
    public void EnumerateChildren<T>(Func<T, bool> predicate, Action<T> action, string namePattern) where T : SceneNode
    {
        foreach (var child in EnumerateChildren<T>(namePattern).Where(predicate))
            action(child);
    }

    /// <summary>
    /// Enumerates child nodes of the specified type that match the specified predicate and whose name matches the provided wildcard pattern and flags, performing the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="predicate">The predicate child nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching child node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that child names must match.</param>
    /// <param name="filter">The flags child nodes must contain to be visited.</param>
    public void EnumerateChildren<T>(Func<T, bool> predicate, Action<T> action, string namePattern, SceneNodeFlags filter) where T : SceneNode
    {
        foreach (var child in EnumerateChildren<T>(namePattern, filter).Where(predicate))
            action(child);
    }

    /// <summary>
    /// Enumerates descendant nodes of the specified type whose name matches the provided wildcard pattern.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that descendant names must match.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of matching descendant nodes.</returns>
    public IEnumerable<T> EnumerateDescendants<T>(string namePattern) where T : SceneNode =>
        EnumerateDescendants<T>(namePattern, SceneNodeFlags.None);

    /// <summary>
    /// Enumerates descendant nodes of the specified type whose name matches the provided wildcard pattern and that match the provided filter.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that descendant names must match.</param>
    /// <param name="filter">The flags descendant nodes must contain to be returned.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of matching descendant nodes.</returns>
    public IEnumerable<T> EnumerateDescendants<T>(string namePattern, SceneNodeFlags filter) where T : SceneNode =>
        EnumerateDescendants<T>(filter).Where(x => x.MatchesName(namePattern));

    /// <summary>
    /// Enumerates descendant nodes of the specified type whose name matches the provided wildcard pattern and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="action">The action to perform on each matching descendant node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that descendant names must match.</param>
    public void EnumerateDescendants<T>(Action<T> action, string namePattern) where T : SceneNode
    {
        foreach (var descendant in EnumerateDescendants<T>(namePattern))
            action(descendant);
    }

    /// <summary>
    /// Enumerates descendant nodes of the specified type whose name matches the provided wildcard pattern and that match the provided filter, performing the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="action">The action to perform on each matching descendant node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that descendant names must match.</param>
    /// <param name="filter">The flags descendant nodes must contain to be visited.</param>
    public void EnumerateDescendants<T>(Action<T> action, string namePattern, SceneNodeFlags filter) where T : SceneNode
    {
        foreach (var descendant in EnumerateDescendants<T>(namePattern, filter))
            action(descendant);
    }

    /// <summary>
    /// Enumerates descendant nodes of the specified type that match the specified predicate and whose name matches the provided wildcard pattern, performing the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="predicate">The predicate descendant nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching descendant node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that descendant names must match.</param>
    public void EnumerateDescendants<T>(Func<T, bool> predicate, Action<T> action, string namePattern) where T : SceneNode
    {
        foreach (var descendant in EnumerateDescendants<T>(namePattern).Where(predicate))
            action(descendant);
    }

    /// <summary>
    /// Enumerates descendant nodes of the specified type that match the specified predicate and whose name matches the provided wildcard pattern and flags, performing the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="predicate">The predicate descendant nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching descendant node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that descendant names must match.</param>
    /// <param name="filter">The flags descendant nodes must contain to be visited.</param>
    public void EnumerateDescendants<T>(Func<T, bool> predicate, Action<T> action, string namePattern, SceneNodeFlags filter) where T : SceneNode
    {
        foreach (var descendant in EnumerateDescendants<T>(namePattern, filter).Where(predicate))
            action(descendant);
    }

    /// <summary>
    /// Enumerates ancestor nodes of the specified type whose name matches the provided wildcard pattern.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that ancestor names must match.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of matching ancestor nodes.</returns>
    public IEnumerable<T> EnumerateAncestors<T>(string namePattern) where T : SceneNode =>
        EnumerateAncestors<T>(namePattern, SceneNodeFlags.None);

    /// <summary>
    /// Enumerates ancestor nodes of the specified type whose name matches the provided wildcard pattern and that match the provided filter.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that ancestor names must match.</param>
    /// <param name="filter">The flags ancestor nodes must contain to be returned.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of matching ancestor nodes.</returns>
    public IEnumerable<T> EnumerateAncestors<T>(string namePattern, SceneNodeFlags filter) where T : SceneNode =>
        EnumerateAncestors<T>(filter).Where(x => x.MatchesName(namePattern));

    /// <summary>
    /// Enumerates ancestor nodes of the specified type whose name matches the provided wildcard pattern and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="action">The action to perform on each matching ancestor node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that ancestor names must match.</param>
    public void EnumerateAncestors<T>(Action<T> action, string namePattern) where T : SceneNode
    {
        foreach (var ancestor in EnumerateAncestors<T>(namePattern))
            action(ancestor);
    }

    /// <summary>
    /// Enumerates ancestor nodes of the specified type whose name matches the provided wildcard pattern and that match the provided filter, performing the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="action">The action to perform on each matching ancestor node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that ancestor names must match.</param>
    /// <param name="filter">The flags ancestor nodes must contain to be visited.</param>
    public void EnumerateAncestors<T>(Action<T> action, string namePattern, SceneNodeFlags filter) where T : SceneNode
    {
        foreach (var ancestor in EnumerateAncestors<T>(namePattern, filter))
            action(ancestor);
    }

    /// <summary>
    /// Enumerates ancestor nodes of the specified type that match the specified predicate and whose name matches the provided wildcard pattern, performing the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="predicate">The predicate ancestor nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching ancestor node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that ancestor names must match.</param>
    public void EnumerateAncestors<T>(Func<T, bool> predicate, Action<T> action, string namePattern) where T : SceneNode
    {
        foreach (var ancestor in EnumerateAncestors<T>(namePattern).Where(predicate))
            action(ancestor);
    }

    /// <summary>
    /// Enumerates ancestor nodes of the specified type that match the specified predicate and whose name matches the provided wildcard pattern and flags, performing the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="predicate">The predicate ancestor nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching ancestor node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that ancestor names must match.</param>
    /// <param name="filter">The flags ancestor nodes must contain to be visited.</param>
    public void EnumerateAncestors<T>(Func<T, bool> predicate, Action<T> action, string namePattern, SceneNodeFlags filter) where T : SceneNode
    {
        foreach (var ancestor in EnumerateAncestors<T>(namePattern, filter).Where(predicate))
            action(ancestor);
    }

    /// <summary>
    /// Enumerates this node and all descendants of the specified type whose name matches the provided wildcard pattern.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that node names must match.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of matching nodes.</returns>
    public IEnumerable<T> EnumerateHierarchy<T>(string namePattern) where T : SceneNode =>
        EnumerateHierarchy<T>(namePattern, SceneNodeFlags.None);

    /// <summary>
    /// Enumerates this node and all descendants of the specified type whose name matches the provided wildcard pattern and that match the provided filter.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that node names must match.</param>
    /// <param name="filter">The flags nodes must contain to be returned.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of matching nodes.</returns>
    public IEnumerable<T> EnumerateHierarchy<T>(string namePattern, SceneNodeFlags filter) where T : SceneNode =>
        EnumerateHierarchy<T>(filter).Where(x => x.MatchesName(namePattern));

    /// <summary>
    /// Enumerates this node and descendants of the specified type whose name matches the provided wildcard pattern and performs the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="action">The action to perform on each matching node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that node names must match.</param>
    public void EnumerateHierarchy<T>(Action<T> action, string namePattern) where T : SceneNode
    {
        foreach (var node in EnumerateHierarchy<T>(namePattern))
            action(node);
    }

    /// <summary>
    /// Enumerates this node and descendants of the specified type whose name matches the provided wildcard pattern and that match the provided filter, performing the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="action">The action to perform on each matching node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that node names must match.</param>
    /// <param name="filter">The flags nodes must contain to be visited.</param>
    public void EnumerateHierarchy<T>(Action<T> action, string namePattern, SceneNodeFlags filter) where T : SceneNode
    {
        foreach (var node in EnumerateHierarchy<T>(namePattern, filter))
            action(node);
    }

    /// <summary>
    /// Enumerates this node and descendants of the specified type that match the specified predicate and whose name matches the provided wildcard pattern, performing the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="predicate">The predicate nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that node names must match.</param>
    public void EnumerateHierarchy<T>(Func<T, bool> predicate, Action<T> action, string namePattern) where T : SceneNode
    {
        foreach (var node in EnumerateHierarchy<T>(namePattern).Where(predicate))
            action(node);
    }

    /// <summary>
    /// Enumerates this node and descendants of the specified type that match the specified predicate and whose name matches the provided wildcard pattern and flags, performing the specified action on each.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="predicate">The predicate nodes must satisfy to be visited.</param>
    /// <param name="action">The action to perform on each matching node.</param>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that node names must match.</param>
    /// <param name="filter">The flags nodes must contain to be visited.</param>
    public void EnumerateHierarchy<T>(Func<T, bool> predicate, Action<T> action, string namePattern, SceneNodeFlags filter) where T : SceneNode
    {
        foreach (var node in EnumerateHierarchy<T>(namePattern, filter).Where(predicate))
            action(node);
    }

    /// <summary>
    /// Returns an array containing all descendant nodes whose name matches the provided wildcard pattern.
    /// </summary>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that descendant names must match.</param>
    public SceneNode[] GetDescendants(string namePattern) => [.. EnumerateDescendants(namePattern)];

    /// <summary>
    /// Returns an array containing all descendant nodes whose name matches the provided wildcard pattern and that match the provided filter.
    /// </summary>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that descendant names must match.</param>
    /// <param name="filter">The flags descendant nodes must contain to be returned.</param>
    public SceneNode[] GetDescendants(string namePattern, SceneNodeFlags filter) => [.. EnumerateDescendants(namePattern, filter)];

    /// <summary>
    /// Returns an array containing all ancestor nodes whose name matches the provided wildcard pattern.
    /// </summary>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that ancestor names must match.</param>
    public SceneNode[] GetAncestors(string namePattern) => [.. EnumerateAncestors(namePattern)];

    /// <summary>
    /// Returns an array containing all ancestor nodes whose name matches the provided wildcard pattern and that match the provided filter.
    /// </summary>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that ancestor names must match.</param>
    /// <param name="filter">The flags ancestor nodes must contain to be returned.</param>
    public SceneNode[] GetAncestors(string namePattern, SceneNodeFlags filter) => [.. EnumerateAncestors(namePattern, filter)];

    /// <summary>
    /// Returns an array containing all sibling nodes whose name matches the provided wildcard pattern.
    /// </summary>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that sibling names must match.</param>
    public SceneNode[] GetSiblings(string namePattern) => [.. EnumerateSiblings(namePattern)];

    /// <summary>
    /// Returns an array containing all sibling nodes whose name matches the provided wildcard pattern and that match the provided filter.
    /// </summary>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that sibling names must match.</param>
    /// <param name="filter">The flags sibling nodes must contain to be returned.</param>
    public SceneNode[] GetSiblings(string namePattern, SceneNodeFlags filter) => [.. EnumerateSiblings(namePattern, filter)];

    /// <summary>
    /// Returns an array containing all descendant nodes of the specified type whose name matches the provided wildcard pattern.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that descendant names must match.</param>
    public T[] GetDescendants<T>(string namePattern) where T : SceneNode => [.. EnumerateDescendants<T>(namePattern)];

    /// <summary>
    /// Returns an array containing all descendant nodes of the specified type whose name matches the provided wildcard pattern and that match the provided filter.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that descendant names must match.</param>
    /// <param name="filter">The flags descendant nodes must contain to be returned.</param>
    public T[] GetDescendants<T>(string namePattern, SceneNodeFlags filter) where T : SceneNode => [.. EnumerateDescendants<T>(namePattern, filter)];

    /// <summary>
    /// Returns an array containing all ancestor nodes of the specified type whose name matches the provided wildcard pattern.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that ancestor names must match.</param>
    public T[] GetAncestors<T>(string namePattern) where T : SceneNode => [.. EnumerateAncestors<T>(namePattern)];

    /// <summary>
    /// Returns an array containing all ancestor nodes of the specified type whose name matches the provided wildcard pattern and that match the provided filter.
    /// </summary>
    /// <typeparam name="T">The node type to filter for.</typeparam>
    /// <param name="namePattern">The wildcard pattern (using <c>*</c> and <c>?</c>) that ancestor names must match.</param>
    /// <param name="filter">The flags ancestor nodes must contain to be returned.</param>
    public T[] GetAncestors<T>(string namePattern, SceneNodeFlags filter) where T : SceneNode => [.. EnumerateAncestors<T>(namePattern, filter)];

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
    /// Sets a custom attribute with the given key and value.
    /// </summary>
    /// <param name="key">The key/name of the attribute.</param>
    /// <param name="value">The value of the attribute.</param>
    public void SetAttribute(string key, object value)
    {
        Attributes ??= [];
        Attributes[key] = value;
    }

    /// <summary>
    /// Attempts to get a custom attribute by key.
    /// </summary>
    /// <param name="key">The key/name of the attribute.</param>
    /// <param name="value">The value of the attribute.</param>
    /// <returns><see langword="true"/> if the attribute was found; otherwise <see langword="false"/>.</returns>
    public bool TryGetAttribute(string key, [NotNullWhen(true)] out object? value)
    {
        value = null;
        return Attributes != null && Attributes.TryGetValue(key, out value);
    }

    /// <summary>
    /// Attempts to get a custom attribute by key.
    /// </summary>
    /// <typeparam name="T">The type of the attribute.</typeparam>
    /// <param name="key">The key/name of the attribute.</param>
    /// <param name="value">The value of the attribute.</param>
    /// <returns><see langword="true"/> if the attribute was found; otherwise <see langword="false"/>.</returns>
    public bool TryGetAttribute<T>(string key, [NotNullWhen(true)] out T? value)
    {
        value = default;
        if (Attributes != null && Attributes.TryGetValue(key, out var objValue) && objValue is T tValue)
        {
            value = tValue;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a custom attribute by key.
    /// </summary>
    /// <param name="key">The key/name of the attribute.</param>
    /// <returns>The value of the attribute.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the attribute with the specified key is not found.</exception>
    public object GetAttribute(string key)
    {
        if (Attributes != null && Attributes.TryGetValue(key, out var value))
        {
            return value;
        }
        throw new KeyNotFoundException($"Attribute with key '{key}' not found.");
    }

    /// <summary>
    /// Gets a custom attribute by key.
    /// </summary>
    /// <typeparam name="T">The type of the attribute.</typeparam>
    /// <param name="key">The key/name of the attribute.</param>
    /// <returns>The value of the attribute.</returns>
    /// <exception cref="InvalidCastException">Thrown when the attribute with the specified key is not of the expected type.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the attribute with the specified key is not found.</exception>
    public T GetAttribute<T>(string key)
    {
        if (Attributes != null && Attributes.TryGetValue(key, out var value))
        {
            if (value is T tValue)
            {
                return tValue;
            }
            throw new InvalidCastException($"Attribute with key '{key}' is not of type {typeof(T).FullName}.");
        }
        throw new KeyNotFoundException($"Attribute with key '{key}' not found.");
    }

    /// <summary>
    /// Gets a custom attribute by key.
    /// </summary>
    /// <param name="key">The key/name of the attribute.</param>
    /// <param name="defaultValue">The default value to return if the attribute is not found.</param>
    /// <returns>The value of the attribute.</returns>
    public object GetAttribute(string key, object defaultValue)
    {
        if (Attributes != null && Attributes.TryGetValue(key, out var value))
        {
            return value;
        }
        return defaultValue;
    }

    /// <summary>
    /// Gets a custom attribute by key.
    /// </summary>
    /// <typeparam name="T">The type of the attribute.</typeparam>
    /// <param name="key">The key/name of the attribute.</param>
    /// <param name="defaultValue">The default value to return if the attribute is not found.</param>
    /// <returns>The value of the attribute.</returns>
    /// <exception cref="InvalidCastException">Thrown when the attribute with the specified key is not of the expected type.</exception>
    public T GetAttribute<T>(string key, T defaultValue)
    {
        if (Attributes != null && Attributes.TryGetValue(key, out var value))
        {
            if (value is T tValue)
            {
                return tValue;
            }
            throw new InvalidCastException($"Attribute with key '{key}' is not of type {typeof(T).FullName}.");
        }
        return defaultValue;
    }

    /// <summary>
    /// Gets the user data associated with this node, cast to the specified type.
    /// </summary>
    /// <typeparam name="T">The type to cast the user data to.</typeparam>
    /// <returns>The user data cast to the specified type.</returns>
    /// <exception cref="NullReferenceException">Thrown when the user data is null.</exception>
    /// <exception cref="InvalidCastException">Thrown when the user data is not of the expected type.</exception>
    public T GetUserData<T>()
    {
        if (UserData is null)
            throw new NullReferenceException("UserData is null.");
        if (UserData is not T data)
            throw new InvalidCastException($"UserData is not of type {typeof(T).FullName}.");
        return data;
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

    internal void SetScene(Scene? scene)
    {
        _scene = scene;
        if (_children is null)
        {
            return;
        }

        for (int i = 0; i < _children.Count; i++)
        {
            _children[i].SetScene(scene);
        }
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
