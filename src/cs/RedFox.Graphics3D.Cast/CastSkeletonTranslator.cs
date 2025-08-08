using Cast.NET.Nodes;
using RedFox.Graphics3D.Skeletal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.Graphics3D.Cast
{
    public static class CastSkeletonTranslator
    {
        public static Skeleton TranslateFrom(Graphics3DScene scene, SkeletonNode skeletonNode, string name)
        {
            var skeleton = scene.AddObject<Skeleton>(name);
            var boneNodes = skeletonNode.GetChildrenOfType<BoneNode>();

            foreach (var boneNode in boneNodes)
            {
                skeleton.AddBone(scene.AddObject<SkeletonBone>(boneNode.Name));
            }

            for (int i = 0; i < boneNodes.Length && i < skeleton.Bones.Count; i++)
            {
                var boneNode = boneNodes[i];
                var bone = skeleton.Bones[i];
                var parentIndex = boneNode.ParentIndex;

                bone.MoveTo(parentIndex > -1 ? skeleton.Bones[parentIndex] : null);

                if (boneNode.TryGetLocalPosition(out var localPosition))
                    bone.BaseTransform.LocalPosition = localPosition;
                if (boneNode.TryGetLocalRotation(out var localRotation))
                    bone.BaseTransform.LocalRotation = localRotation;
                if (boneNode.TryGetWorldPosition(out var worldPosition))
                    bone.BaseTransform.WorldPosition = worldPosition;
                if (boneNode.TryGetWorldRotation(out var worldRotation))
                    bone.BaseTransform.WorldRotation = worldRotation;
            }

            return skeleton;
        }
    }
}
