using System.Numerics;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats;

internal static class SkeletonExportTransforms
{
    internal static void GetRelativeBindTransform(SkeletonBone bone, SceneNode[] exportedBoneNodes, out Vector3 position, out Quaternion rotation)
    {
        Vector3 worldPosition = bone.GetBindWorldPosition();
        Quaternion worldRotation = Quaternion.Normalize(bone.GetBindWorldRotation());

        if (SceneNode.GetBestParent(bone, exportedBoneNodes) is SkeletonBone exportedParent)
        {
            Vector3 parentWorldPosition = exportedParent.GetBindWorldPosition();
            Quaternion parentWorldRotation = Quaternion.Normalize(exportedParent.GetBindWorldRotation());
            position = Vector3.Transform(worldPosition - parentWorldPosition, Quaternion.Conjugate(parentWorldRotation));
            rotation = Quaternion.Normalize(Quaternion.Conjugate(parentWorldRotation) * worldRotation);
            return;
        }

        position = worldPosition;
        rotation = worldRotation;
    }

    internal static void GetRelativeAnimatedTransform(SkeletonBone bone, IReadOnlyDictionary<SkeletonBone, SkeletonBone?> exportedParents, IReadOnlyDictionary<string, SkeletonAnimationTrack> tracksByName, float time, Dictionary<SkeletonBone, (Vector3 Position, Quaternion Rotation)> worldTransforms, out Vector3 position, out Quaternion rotation)
    {
        ComputeAnimatedWorldTransform(bone, tracksByName, time, worldTransforms, out Vector3 worldPosition, out Quaternion worldRotation);

        if (exportedParents.TryGetValue(bone, out SkeletonBone? exportedParent) && exportedParent is not null)
        {
            ComputeAnimatedWorldTransform(exportedParent, tracksByName, time, worldTransforms, out Vector3 parentWorldPosition, out Quaternion parentWorldRotation);
            position = Vector3.Transform(worldPosition - parentWorldPosition, Quaternion.Conjugate(parentWorldRotation));
            rotation = Quaternion.Normalize(Quaternion.Conjugate(parentWorldRotation) * worldRotation);
            return;
        }

        position = worldPosition;
        rotation = worldRotation;
    }

    internal static void ComputeAnimatedWorldTransform(SkeletonBone bone, IReadOnlyDictionary<string, SkeletonAnimationTrack> tracksByName, float time, Dictionary<SkeletonBone, (Vector3 Position, Quaternion Rotation)> worldTransforms, out Vector3 worldPosition, out Quaternion worldRotation)
    {
        if (worldTransforms.TryGetValue(bone, out (Vector3 Position, Quaternion Rotation) cached))
        {
            worldPosition = cached.Position;
            worldRotation = cached.Rotation;
            return;
        }

        Vector3 localPosition = bone.GetBindLocalPosition();
        Quaternion localRotation = Quaternion.Normalize(bone.GetBindLocalRotation());

        if (tracksByName.TryGetValue(bone.Name, out SkeletonAnimationTrack? track))
        {
            if (track.TranslationCurve is { KeyFrameCount: > 0 } translationCurve)
                localPosition = translationCurve.SampleVector3(time);
            if (track.RotationCurve is { KeyFrameCount: > 0 } rotationCurve)
                localRotation = rotationCurve.SampleQuaternion(time);
        }

        if (bone.Parent is SkeletonBone parentBone)
        {
            ComputeAnimatedWorldTransform(parentBone, tracksByName, time, worldTransforms, out Vector3 parentWorldPosition, out Quaternion parentWorldRotation);
            worldRotation = Quaternion.Normalize(parentWorldRotation * localRotation);
            worldPosition = parentWorldPosition + Vector3.Transform(localPosition, parentWorldRotation);
        }
        else
        {
            worldPosition = localPosition;
            worldRotation = localRotation;
        }

        worldTransforms[bone] = (worldPosition, worldRotation);
    }

    internal static Dictionary<SkeletonBone, SkeletonBone?> BuildExportedParentMap(IReadOnlyList<SkeletonBone> bones, SceneNode[] exportedBoneNodes)
    {
        HashSet<SceneNode> exported = [.. exportedBoneNodes];
        Dictionary<SkeletonBone, SkeletonBone?> parents = new(bones.Count);
        foreach (SkeletonBone bone in bones)
        {
            SceneNode? parent = bone.Parent;
            while (parent is not null && !exported.Contains(parent))
                parent = parent.Parent;
            parents[bone] = parent as SkeletonBone;
        }

        return parents;
    }
}
