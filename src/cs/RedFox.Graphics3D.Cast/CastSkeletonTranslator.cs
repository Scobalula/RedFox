using Cast.NET.Nodes;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Cast;

internal static class CastSkeletonTranslator
{
    public static SkeletonBone[] Read(SceneNode parent, SkeletonNode skeletonNode, string name)
    {
        var boneNodes = skeletonNode.GetChildrenOfType<BoneNode>();
        var bones = new SkeletonBone[boneNodes.Length];

        // First pass — create all bones
        for (int i = 0; i < boneNodes.Length; i++)
        {
            bones[i] = new SkeletonBone(boneNodes[i].Name);
        }

        // Second pass — set up hierarchy and transforms
        for (int i = 0; i < boneNodes.Length; i++)
        {
            var boneNode = boneNodes[i];
            var bone = bones[i];
            int parentIndex = boneNode.ParentIndex;

            bone.MoveTo(parentIndex >= 0 && parentIndex < bones.Length ? bones[parentIndex] : parent, ReparentTransformMode.PreserveExisting);

            if (boneNode.TryGetLocalPosition(out var localPosition))
                bone.BindTransform.LocalPosition = localPosition;
            if (boneNode.TryGetLocalRotation(out var localRotation))
                bone.BindTransform.LocalRotation = localRotation;
            if (boneNode.TryGetWorldPosition(out var worldPosition))
                bone.BindTransform.WorldPosition = worldPosition;
            if (boneNode.TryGetWorldRotation(out var worldRotation))
                bone.BindTransform.WorldRotation = worldRotation;
        }

        return bones;
    }
}
