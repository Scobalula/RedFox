using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D;

/// <summary>
/// Combines model scenes without modifying their source hierarchies.
/// </summary>
public static class SceneCombiner
{
    /// <summary>
    /// Copies models into a new scene using the selected bone-sharing mode.
    /// </summary>
    /// <param name="scenes">The models to combine, in bone-precedence order.</param>
    /// <param name="mode">How model skeletons are combined.</param>
    /// <param name="cancellationToken">The token used to cancel composition.</param>
    /// <returns>A new scene containing copies of the source models.</returns>
    public static Scene Combine(IReadOnlyList<Scene> scenes, SceneCombineMode mode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scenes);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        Scene result = new(scenes.Count == 1 ? scenes[0].Name : "Combined preview");
        Dictionary<string, SkeletonBone> canonical = new(StringComparer.Ordinal);
        foreach (Scene source in scenes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Group model = result.AddNode(new Group($"Model_{result.EnumerateChildren().Count()}"));
            source.BindTransform.CopyTo(model.BindTransform);
            model.Flags = source.Flags;
            source.CloneChildrenTo(model, cancellationToken);
            foreach (var translator in source.ImageTranslators.Translators)
            {
                result.ImageTranslators.Register(translator);
            }
            if (mode != SceneCombineMode.ConnectBones || scenes.Count < 2)
            {
                continue;
            }

            SkeletonBone[] bones	= model.EnumerateDescendants<SkeletonBone>().ToArray();
            string? duplicate		= bones.GroupBy(bone => bone.Name, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1)?.Key;
            if (duplicate is not null)
            {
                throw new InvalidOperationException($"Ambiguous bone '{duplicate}'. Use Merge in place for this model.");
            }
            foreach (Mesh mesh in model.EnumerateDescendants<Mesh>())
            {
                if (mesh.Skin is { InverseBindMatrices: null } skin)
                {
                    skin.CaptureInverseBindMatrices(mesh.GetBindWorldMatrix());
                }
            }
            foreach (SkeletonBone bone in bones)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!canonical.TryGetValue(bone.Name, out SkeletonBone? existing))
                {
                    canonical.Add(bone.Name, bone);
                    continue;
                }
                foreach (SceneNode child in bone.EnumerateChildren().ToArray())
                {
                    child.MoveTo(existing, child is SkeletonBone ? ReparentTransformMode.PreserveLocal : ReparentTransformMode.PreserveWorld, SceneNodeMatchScope.None, SceneMergeStrategy.Throw);
                }
                SceneNode.RedirectReferences(bone, existing, result, model);
                bone.Detach();
            }
        }
        return result;
    }

    /// <summary>
    /// Binds embedded clips to their models and external clips to compatible targets.
    /// </summary>
    /// <param name="scene">The combined scene whose animation players are rebuilt.</param>
    /// <param name="mode">The skeleton-sharing mode used to combine the models.</param>
    /// <param name="cancellationToken">The token used to cancel animation binding.</param>
    public static void BindAnimations(Scene scene, SceneCombineMode mode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scene);
        cancellationToken.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        SceneNode[] skeletons = scene.EnumerateDescendants().Where(node => node is Skeleton || node is SkeletonBone && node.Parent is not SkeletonBone and not Skeleton).ToArray();
        Dictionary<Animation, IReadOnlyList<SceneNode>> targets = [];
        foreach (Animation animation in scene.EnumerateDescendants<Animation>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            SceneNode owner = animation;
            while (owner.Parent is { } parent && !ReferenceEquals(parent, scene))
            {
                owner = parent;
            }
            bool external = ReferenceEquals(owner, animation);
            if (animation is SkeletonAnimation skeletal)
            {
                targets[animation] = skeletons.Where(root => (mode == SceneCombineMode.ConnectBones || external || root.EnumerateAncestors().Contains(owner) || ReferenceEquals(root, owner)) && root.EnumerateHierarchy<SkeletonBone>().Any(bone => skeletal.Tracks.Any(track => track.Name == bone.Name))).ToArray();
            }
            else
            {
                targets[animation] = external ? [scene] : [owner];
            }
        }
        scene.CreateAnimationPlayers(targets, cancellationToken);
    }
}
