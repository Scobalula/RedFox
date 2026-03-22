namespace RedFox.Graphics3D;

public class Skeleton : SceneNode
{
    public Skeleton() : base() { }

    public Skeleton(string name) : base(name) { }

    public Skeleton(string name, SceneNodeFlags flags) : base(name, flags) { }

    public IEnumerable<SkeletonBone> EnumerateBones() => EnumerateDescendants().OfType<SkeletonBone>();

    public SkeletonBone[] GetBones() => [..EnumerateBones()];

    public bool TryFindBone(string name, out SkeletonBone? bone)
        => TryFindBone(name, StringComparison.CurrentCulture, out bone);

    public bool TryFindBone(string name, StringComparison comparisonType, out SkeletonBone? bone)
        => TryFindDescendant(name, comparisonType, out bone);

    public SkeletonBone FindBone(string name) => FindBone(name, StringComparison.CurrentCulture);

    private SkeletonBone FindBone(string name, StringComparison comparisonType)
    {
        if (TryFindDescendant(name, comparisonType, out SkeletonBone? node))
            return node;

        throw new KeyNotFoundException($"A bone with the name: {name} was not found in: {Name}");
    }


    public void MergeByName(Skeleton source)
        => MergeByName(source, StringComparison.OrdinalIgnoreCase);

    public void MergeByName(Skeleton source, StringComparison comparison)
    {
        // Snapshot the source bones before mutating the tree.
        SkeletonBone[] sourceBones = source.GetBones();
        Dictionary<SkeletonBone, SkeletonBone> remappedBones = [];

        SceneNode targetRoot = GetRoot();
        SceneNode sourceRoot = source.GetRoot();

        foreach (SkeletonBone srcBone in sourceBones)
        {
            if (!TryFindBone(srcBone.Name, comparison, out var targetBone) || targetBone is null)
                continue;

            remappedBones[srcBone] = targetBone;

            // Move all of the source bone's children under the matched target bone.
            SceneNode[] children = srcBone.EnumerateChildren().ToArray();
            foreach (SceneNode child in children)
                child.MoveTo(targetBone, ReparentTransformMode.PreserveLocal);

            // Remove the now-empty source bone from its parent.
            srcBone.Detach();
        }

        if (remappedBones.Count > 0)
            RemapMergedBones(remappedBones, targetRoot, sourceRoot);
    }

    /// <summary>
    /// Connects a <paramref name="source"/> skeleton (or a specific bone subtree
    /// within it) under the bone named <paramref name="targetBoneName"/> in this
    /// skeleton.
    /// </summary>
    /// <param name="source">The imported skeleton to attach.</param>
    /// <param name="targetBoneName">
    /// The name of the bone in <b>this</b> skeleton that should become the parent
    /// of the imported subtree.
    /// </param>
    /// <param name="sourceBoneName">
    /// If non-null, only this bone's subtree within <paramref name="source"/> is
    /// attached. When <c>null</c>, all top-level children of <paramref name="source"/>
    /// are attached.
    /// </param>
    /// <param name="comparison">String comparison mode for bone name lookup.</param>
    public void ConnectToTarget(
        Skeleton source,
        string targetBoneName)
        => ConnectToTarget(source, targetBoneName, sourceBoneName: null, StringComparison.OrdinalIgnoreCase);

    public void ConnectToTarget(
        Skeleton source,
        string targetBoneName,
        string? sourceBoneName)
        => ConnectToTarget(source, targetBoneName, sourceBoneName, StringComparison.OrdinalIgnoreCase);

    public void ConnectToTarget(
        Skeleton source,
        string targetBoneName,
        string? sourceBoneName,
        StringComparison comparison)
    {
        if (!TryFindBone(targetBoneName, comparison, out var targetBone) || targetBone is null)
            throw new KeyNotFoundException(
                $"Target bone '{targetBoneName}' not found in skeleton '{Name}'.");

        if (sourceBoneName is not null)
        {
            if (!source.TryFindBone(sourceBoneName, comparison, out var srcBone) || srcBone is null)
                throw new KeyNotFoundException(
                    $"Source bone '{sourceBoneName}' not found in skeleton '{source.Name}'.");

            srcBone.MoveTo(targetBone, ReparentTransformMode.PreserveLocal);
        }
        else
        {
            var children = source.EnumerateChildren().ToArray();
            foreach (var child in children)
                child.MoveTo(targetBone, ReparentTransformMode.PreserveLocal);
        }
    }

    /// <summary>
    /// Prefixes all bone names in this skeleton with the given
    /// <paramref name="ns"/> namespace string.
    /// </summary>
    public void ApplyNamespace(string ns)
    {
        if (string.IsNullOrEmpty(ns))
            return;

        foreach (SkeletonBone bone in EnumerateBones())
            bone.Name = ns + bone.Name;
    }

    private static void RemapMergedBones(IReadOnlyDictionary<SkeletonBone, SkeletonBone> remappedBones, SceneNode targetRoot, SceneNode sourceRoot)
    {
        HashSet<SceneNode> visitedRoots = [];
        HashSet<Mesh> visitedMeshes = [];

        RemapMergedBones(remappedBones, targetRoot, visitedRoots, visitedMeshes);
        RemapMergedBones(remappedBones, sourceRoot, visitedRoots, visitedMeshes);
    }

    private static void RemapMergedBones(
        IReadOnlyDictionary<SkeletonBone, SkeletonBone> remappedBones,
        SceneNode root,
        ISet<SceneNode> visitedRoots,
        ISet<Mesh> visitedMeshes)
    {
        if (!visitedRoots.Add(root))
            return;

        foreach (Mesh mesh in root.EnumerateHierarchy<Mesh>())
        {
            if (!visitedMeshes.Add(mesh))
                continue;

            mesh.RemapSkinnedBones(remappedBones);
        }
    }
}
