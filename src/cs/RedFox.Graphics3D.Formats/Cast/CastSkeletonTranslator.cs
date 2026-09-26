using CastNet.Nodes;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats.Cast;

internal static class CastSkeletonTranslator
{
    public static SkeletonBone[] Read(SceneNode parent, SkeletonNode skeletonNode, string name)
    {
        var skeleton = parent.AddNode(new Skeleton(name));
        var boneNodes = skeletonNode.Bones;
        var bones = Array.ConvertAll(boneNodes, boneNode => new SkeletonBone(boneNode.Name));

        for (var i = 0; i < boneNodes.Length; i++)
        {
            var parentIndex = boneNodes[i].ParentIndex;
            var transform = bones[i].BindTransform;

            bones[i].MoveTo((uint)parentIndex < (uint)bones.Length ? bones[parentIndex] : skeleton,ReparentTransformMode.PreserveExisting);

            transform.LocalPosition = boneNodes[i].LocalPosition;
            transform.LocalRotation = boneNodes[i].LocalRotation;
            transform.WorldPosition = boneNodes[i].WorldPosition;
            transform.WorldRotation = boneNodes[i].WorldRotation;
            transform.Scale = boneNodes[i].Scale;
        }

        return bones;
    }
}
