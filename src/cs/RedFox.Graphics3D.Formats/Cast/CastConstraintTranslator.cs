using CastNet.Nodes;
using RedFox.Graphics3D.Groups;
using RedFox.Graphics3D.IO;
using RedFox.Graphics3D.Skeletal;
using System.Numerics;
using CastConstraintNode	= CastNet.Nodes.ConstraintNode;
using CastIKHandleNode		= CastNet.Nodes.IKHandleNode;

namespace RedFox.Graphics3D.Formats.Cast;

internal static class CastConstraintTranslator
{
    public static void Read(MeshGroup model, SkeletonNode skeletonNode, SkeletonBone[] bones)
    {
        var boneNodes	= skeletonNode.Bones;
        var boneLookup	= new Dictionary<BoneNode, SkeletonBone>(boneNodes.Length);

        for (var i = 0; i < boneNodes.Length; i++)
            boneLookup[boneNodes[i]] = bones[i];

        foreach (var handle in skeletonNode.EnumerateIKHandles())
        {
            if (handle.StartBone is not BoneNode startBone || handle.EndBone is not BoneNode endBone)
                continue;

            model.AddNode(new IKHandleNode(CastTranslator.GetUniqueName(model, handle.Name ?? $"{endBone.Name}_ik"), boneLookup[startBone], boneLookup[endBone])
            {
                TargetNode			= handle.TargetBone is BoneNode target ? boneLookup[target] : null,
                TargetOffset		= handle.TargetOffset ?? Vector3.Zero,
                PoleVectorNode		= handle.PoleVectorBone is BoneNode poleVector ? boneLookup[poleVector] : null,
                PoleNode			= handle.PoleBone is BoneNode pole ? boneLookup[pole] : null,
                UseTargetRotation	= handle.UseTargetRotation,
            });
        }

        foreach (var constraint in skeletonNode.EnumerateConstraints())
        {
            if (constraint.ConstraintBone is not BoneNode constrainedBone || constraint.TargetBone is not BoneNode targetBone)
                continue;

            var name			= CastTranslator.GetUniqueName(model, constraint.Name ?? $"{constrainedBone.Name}_{constraint.ConstraintType}");
            var constrained		= boneLookup[constrainedBone];
            var source			= boneLookup[targetBone];
            var hasCustomOffset	= constraint.GetArray("co") is not null;

            ConstraintNode node;
            switch (constraint.ConstraintType)
            {
                case "pt":
                    Vector3 translation = Vector3.Zero;
                    if (hasCustomOffset)
                        translation = constraint.CustomOffset.AsVector3();
                    else if (constraint.MaintainOffset)
                        translation = constrained.GetBindWorldPosition() - source.GetBindWorldPosition();
                    node = new PointConstraintNode(name, constrained, source)
                    {
                        TranslationOffset	= translation,
                        SkipX				= constraint.SkipX,
                        SkipY				= constraint.SkipY,
                        SkipZ				= constraint.SkipZ,
                    };
                    break;
                case "or":
                    Quaternion rotation = Quaternion.Identity;
                    if (hasCustomOffset)
                        rotation = constraint.CustomOffset.AsQuaternion();
                    else if (constraint.MaintainOffset)
                        rotation = Quaternion.Inverse(source.GetBindWorldRotation()) * constrained.GetBindWorldRotation();
                    node = new OrientConstraintNode(name, constrained, source)
                    {
                        RotationOffset	= rotation,
                        SkipX			= constraint.SkipX,
                        SkipY			= constraint.SkipY,
                        SkipZ			= constraint.SkipZ,
                    };
                    break;
                case "sc":
                    Vector3 scale = Vector3.One;
                    if (hasCustomOffset)
                        scale = constraint.CustomOffset.AsVector3();
                    else if (constraint.MaintainOffset)
                        scale = GetBindWorldScale(constrained) / GetBindWorldScale(source);
                    node = new ScaleConstraintNode(name, constrained, source)
                    {
                        ScaleOffset	= scale,
                        SkipX		= constraint.SkipX,
                        SkipY		= constraint.SkipY,
                        SkipZ		= constraint.SkipZ,
                    };
                    break;
                default:
                    continue;
            }
            node.Weight = constraint.Weight;
            model.AddNode(node);
        }
    }

    public static void Write(SkeletonNode skeletonNode, IReadOnlyList<SceneNode> content, Dictionary<SkeletonBone, BoneNode> boneNodes)
    {
        foreach (var handle in content.OfType<IKHandleNode>())
        {
            if (FindBone(boneNodes, handle.StartNode) is not BoneNode startBone || FindBone(boneNodes, handle.EndNode) is not BoneNode endBone)
                continue;

            skeletonNode.AddNode(new CastIKHandleNode
            {
                Name = handle.Name,
                StartBone = startBone,
                EndBone = endBone,
                TargetBone = FindBone(boneNodes, handle.TargetNode),
                TargetOffset = handle.TargetOffset == Vector3.Zero ? null : handle.TargetOffset,
                PoleVectorBone = FindBone(boneNodes, handle.PoleVectorNode),
                PoleBone = FindBone(boneNodes, handle.PoleNode),
                UseTargetRotation = handle.UseTargetRotation,
            });
        }

        foreach (var constraint in content.OfType<ConstraintNode>())
        {
            if (FindBone(boneNodes, constraint.ConstrainedNode) is not BoneNode constrainedBone || FindBone(boneNodes, constraint.SourceNode) is not BoneNode targetBone)
                continue;

            switch (constraint)
            {
                case PointConstraintNode point:
                    AddConstraint(skeletonNode, constraint, constrainedBone, targetBone, "pt", new Vector4(point.TranslationOffset, 0.0f), point.SkipX, point.SkipY, point.SkipZ);
                    break;
                case OrientConstraintNode orient:
                    AddConstraint(skeletonNode, constraint, constrainedBone, targetBone, "or", orient.RotationOffset.AsVector4(), orient.SkipX, orient.SkipY, orient.SkipZ);
                    break;
                case ScaleConstraintNode scale:
                    AddConstraint(skeletonNode, constraint, constrainedBone, targetBone, "sc", new Vector4(scale.ScaleOffset, 0.0f), scale.SkipX, scale.SkipY, scale.SkipZ);
                    break;
                case ParentConstraintNode parent:
                    AddConstraint(skeletonNode, constraint, constrainedBone, targetBone, "pt", new Vector4(parent.TranslationOffset, 0.0f), false, false, false);
                    AddConstraint(skeletonNode, constraint, constrainedBone, targetBone, "or", parent.RotationOffset.AsVector4(), false, false, false);
                    break;
            }
        }
    }

    private static void AddConstraint(SkeletonNode skeletonNode, ConstraintNode constraint, BoneNode constrainedBone, BoneNode targetBone, string type, Vector4 offset, bool skipX, bool skipY, bool skipZ) => skeletonNode.AddNode(new CastConstraintNode
    {
        Name			= constraint.Name,
        ConstraintType	= type,
        ConstraintBone	= constrainedBone,
        TargetBone		= targetBone,
        CustomOffset	= offset,
        Weight			= constraint.Weight,
        SkipX			= skipX,
        SkipY			= skipY,
        SkipZ			= skipZ,
    });

    private static Vector3 GetBindWorldScale(SceneNode node) => Matrix4x4.Decompose(node.GetBindWorldMatrix(), out var scale, out _, out _) ? scale : Vector3.One;

    private static BoneNode? FindBone(Dictionary<SkeletonBone, BoneNode> boneNodes, SceneNode? node) => node is SkeletonBone bone ? boneNodes.GetValueOrDefault(bone) : null;
}
