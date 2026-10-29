using System.Numerics;
using RedFox.Graphics3D.Skeletal;

namespace RedFox.Graphics3D.Formats;

internal static class SkeletonExportTransforms
{
    internal static void GetRelativeBindTransform(SkeletonBone bone, SceneNode? parent, out Vector3 position, out Quaternion rotation)
    {
        DecomposeMatrix(GetRelativeBindMatrix(bone, parent), out Vector3 scale, out rotation, out position);
        if (Vector3.DistanceSquared(scale, Vector3.One) > 0.000001f)
            throw new InvalidDataException($"Bone '{bone.Name}' has a scale that this format cannot represent.");
    }

    internal static Matrix4x4 GetRelativeBindMatrix(SceneNode node, SceneNode? parent)
    {
        Matrix4x4 matrix = node.GetBindWorldMatrix();
        if (parent is not null)
        {
            if (!Matrix4x4.Invert(parent.GetBindWorldMatrix(), out Matrix4x4 inverse))
                throw new InvalidDataException($"Parent '{parent.Name}' has a singular bind transform.");
            matrix *= inverse;
        }
        return matrix;
    }

    internal static Transform DecomposeMatrix(Matrix4x4 matrix)
    {
        DecomposeMatrix(matrix, out Vector3 scale, out Quaternion rotation, out Vector3 position);
        return new Transform
        {
            LocalPosition	= position,
            LocalRotation	= rotation,
            Scale			= scale,
        };
    }

    internal static void GetRelativeAnimatedTransform(SkeletonBone bone, IReadOnlyDictionary<SkeletonBone, SkeletonBone?> exportedParents, IReadOnlyDictionary<string, SkeletonAnimationTrack> tracksByName, float time, Dictionary<SkeletonBone, Matrix4x4> worldTransforms, out Vector3 position, out Quaternion rotation)
    {
        exportedParents.TryGetValue(bone, out SkeletonBone? parent);
        Matrix4x4 matrix = GetRelativeAnimatedMatrix(bone, parent, tracksByName, time, worldTransforms);
        DecomposeMatrix(matrix, out Vector3 scale, out rotation, out position);
        if (Vector3.DistanceSquared(scale, Vector3.One) > 0.000001f)
            throw new InvalidDataException($"Bone '{bone.Name}' has an animated scale that this format cannot represent.");
    }

    internal static Matrix4x4 GetRelativeAnimatedMatrix(SkeletonBone bone, SkeletonBone? parent, IReadOnlyDictionary<string, SkeletonAnimationTrack> tracksByName, float time, Dictionary<SkeletonBone, Matrix4x4> worldTransforms)
    {
        Matrix4x4 matrix = ComputeAnimatedWorldMatrix(bone, tracksByName, time, worldTransforms);
        if (parent is not null)
        {
            if (!Matrix4x4.Invert(ComputeAnimatedWorldMatrix(parent, tracksByName, time, worldTransforms), out Matrix4x4 inverse))
                throw new InvalidDataException($"Parent '{parent.Name}' has a singular animated transform.");
            matrix *= inverse;
        }
        return matrix;
    }

    internal static int[] BuildExportedParentIndices(IReadOnlyList<SkeletonBone> bones, SceneNode[] exportedBoneNodes)
    {
        Dictionary<SceneNode, int> indices = new(exportedBoneNodes.Length);
        for (int i = 0; i < exportedBoneNodes.Length; i++)
            indices.Add(exportedBoneNodes[i], i);
        int[] parents = new int[bones.Count];
        for (int i = 0; i < bones.Count; i++)
        {
            int index			= -1;
            SceneNode? parent	= bones[i].Parent;
            while (parent is not null && !indices.TryGetValue(parent, out index))
                parent = parent.Parent;
            parents[i] = parent is null ? -1 : index;
        }
        return parents;
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

    internal static void DecomposeMatrix(Matrix4x4 matrix, out Vector3 scale, out Quaternion rotation, out Vector3 position)
    {
        if (!Matrix4x4.Decompose(matrix, out scale, out rotation, out position))
            throw new InvalidDataException("The transform cannot be represented as translation, rotation, and scale.");

        Matrix4x4 difference = matrix - Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
        if (!(new Vector4(difference.M11, difference.M12, difference.M13, difference.M14).LengthSquared() + new Vector4(difference.M21, difference.M22, difference.M23, difference.M24).LengthSquared() + new Vector4(difference.M31, difference.M32, difference.M33, difference.M34).LengthSquared() + new Vector4(difference.M41, difference.M42, difference.M43, difference.M44).LengthSquared() < 0.00001f))
            throw new InvalidDataException("The transform contains shear or non-finite values.");
        rotation = Quaternion.Normalize(rotation);
    }

    private static Matrix4x4 ComputeAnimatedWorldMatrix(SkeletonBone bone, IReadOnlyDictionary<string, SkeletonAnimationTrack> tracksByName, float time, Dictionary<SkeletonBone, Matrix4x4> worldTransforms)
    {
        if (worldTransforms.TryGetValue(bone, out Matrix4x4 matrix))
            return matrix;

        Vector3		position	= bone.GetBindLocalPosition();
        Quaternion	rotation	= bone.GetBindLocalRotation();
        Vector3		scale		= bone.BindTransform.Scale ?? Vector3.One;
        if (tracksByName.TryGetValue(bone.Name, out SkeletonAnimationTrack? track))
        {
            if (track.TranslationCurve is { KeyFrameCount: > 0 } translationCurve)
                position = translationCurve.SampleVector3(time);
            if (track.RotationCurve is { KeyFrameCount: > 0 } rotationCurve)
                rotation = rotationCurve.SampleQuaternion(time);
            if (track.ScaleCurve is { KeyFrameCount: > 0 } scaleCurve)
                scale = scaleCurve.SampleVector3(time);
        }

        Matrix4x4 parentMatrix = Matrix4x4.Identity;
        if (bone.Parent is SkeletonBone parentBone)
            parentMatrix = ComputeAnimatedWorldMatrix(parentBone, tracksByName, time, worldTransforms);
        else if (bone.Parent is { } parentNode)
            parentMatrix = parentNode.GetBindWorldMatrix();
        matrix = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(rotation)) * Matrix4x4.CreateTranslation(position) * parentMatrix;
        worldTransforms.Add(bone, matrix);
        return matrix;
    }
}
