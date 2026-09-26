using RedFox.Graphics3D.Buffers;
using System.Numerics;

namespace RedFox.Graphics3D;

/// <summary>
/// Combines several meshes into one while preserving material assignments and skinning.
/// </summary>
/// <remarks>
/// A <see cref="Mesh"/> carries one material set for all of its faces, so only meshes with the same
/// <see cref="Mesh.Materials"/> can be combined into a single mesh; <see cref="CombineByMaterial"/> groups meshes first.
/// Combined geometry is expressed in the space of the first mesh, whose transforms the result copies, so the result
/// can replace the first mesh in the hierarchy. Morph targets are not supported.
/// </remarks>
public static class MeshCombiner
{
    /// <summary>
    /// Combines meshes that share the same materials into a single new, detached mesh.
    /// </summary>
    /// <param name="meshes">The meshes to combine. Each must have positions and face indices, the same materials, and matching vertex layouts.</param>
    /// <returns>The combined mesh, named after and positioned like the first mesh.</returns>
    /// <exception cref="ArgumentException">The meshes are empty, use different materials or layouts, or mix skinned and unskinned meshes.</exception>
    /// <exception cref="NotSupportedException">A mesh has morph targets.</exception>
    public static Mesh Combine(IReadOnlyList<Mesh> meshes)
    {
        ArgumentNullException.ThrowIfNull(meshes);

        if (meshes.Count == 0)
            throw new ArgumentException("At least one mesh is required.", nameof(meshes));

        Mesh first = meshes[0];

        foreach (Mesh mesh in meshes)
            ValidateCompatible(first, mesh);

        Matrix4x4 fromWorld = Matrix4x4.Invert(first.GetBindWorldMatrix(), out Matrix4x4 inverseTargetWorld) ? inverseTargetWorld : Matrix4x4.Identity;
        Matrix4x4[] toTarget = [.. meshes.Select(mesh => mesh.GetBindWorldMatrix() * fromWorld)];

        Mesh result = new()
        {
            Name = first.Name,
            BindTransform = first.BindTransform.Clone(),
            LiveTransform = first.LiveTransform.Clone(),
            Materials = first.Materials is null ? null : [.. first.Materials],
            Positions = CombineVectors(meshes, toTarget, mesh => mesh.Positions, Vector3.Transform),
            Normals = CombineVectors(meshes, toTarget, mesh => mesh.Normals, TransformNormal),
            Tangents = CombineVectors(meshes, toTarget, mesh => mesh.Tangents, TransformDirection),
            BiTangents = CombineVectors(meshes, toTarget, mesh => mesh.BiTangents, TransformDirection),
            ColorLayers = Concat(meshes, mesh => mesh.ColorLayers),
            UVLayers = Concat(meshes, mesh => mesh.UVLayers),
            FaceIndices = CombineFaceIndices(meshes),
        };

        if (first.Skin is not null)
            result.Skin = CombineSkins(meshes, toTarget);

        return result;
    }

    /// <summary>
    /// Groups meshes by their materials and combines each group into a single new, detached mesh.
    /// </summary>
    /// <param name="meshes">The meshes to combine.</param>
    /// <returns>One combined mesh per distinct material set, in order of first appearance.</returns>
    public static IReadOnlyList<Mesh> CombineByMaterial(IReadOnlyList<Mesh> meshes)
    {
        ArgumentNullException.ThrowIfNull(meshes);

        List<List<Mesh>> groups = [];

        foreach (Mesh mesh in meshes)
        {
            List<Mesh>? group = groups.Find(candidate => HaveSameMaterials(candidate[0], mesh));

            if (group is null)
                groups.Add([mesh]);
            else
                group.Add(mesh);
        }

        return [.. groups.Select(Combine)];
    }

    private static void ValidateCompatible(Mesh first, Mesh mesh)
    {
        if (mesh.Positions is null || mesh.FaceIndices is null)
            throw new ArgumentException($"Mesh '{mesh.Name}' must have positions and face indices to be combined.", nameof(mesh));

        if (mesh.Morph is not null)
            throw new NotSupportedException($"Mesh '{mesh.Name}' has morph targets, which cannot be combined.");

        if (!HaveSameMaterials(first, mesh))
            throw new ArgumentException($"Mesh '{mesh.Name}' uses different materials to '{first.Name}'; use {nameof(CombineByMaterial)} instead.", nameof(mesh));

        if ((first.Skin is null) != (mesh.Skin is null))
            throw new ArgumentException($"Mesh '{mesh.Name}' cannot be combined with '{first.Name}' because only one of them is skinned.", nameof(mesh));

        EnsureSameLayout(first, mesh, nameof(Mesh.Normals), first.Normals, mesh.Normals);
        EnsureSameLayout(first, mesh, nameof(Mesh.Tangents), first.Tangents, mesh.Tangents);
        EnsureSameLayout(first, mesh, nameof(Mesh.BiTangents), first.BiTangents, mesh.BiTangents);
        EnsureSameLayout(first, mesh, nameof(Mesh.ColorLayers), first.ColorLayers, mesh.ColorLayers);
        EnsureSameLayout(first, mesh, nameof(Mesh.UVLayers), first.UVLayers, mesh.UVLayers);
    }

    private static void EnsureSameLayout(Mesh first, Mesh mesh, string name, DataBuffer? expected, DataBuffer? actual)
    {
        if (expected is null && actual is null)
            return;

        if (expected is null || actual is null || expected.ValueCount != actual.ValueCount || expected.ComponentCount != actual.ComponentCount)
            throw new ArgumentException($"Mesh '{mesh.Name}' has a {name} layout that does not match '{first.Name}'.", nameof(mesh));
    }

    private static bool HaveSameMaterials(Mesh first, Mesh second) => (first.Materials ?? []).SequenceEqual(second.Materials ?? []);

    private static DataBuffer? CombineVectors(IReadOnlyList<Mesh> meshes, Matrix4x4[] toTarget, Func<Mesh, DataBuffer?> select, Func<Vector3, Matrix4x4, Vector3> transform)
    {
        if (select(meshes[0]) is not { } firstBuffer)
            return null;

        int valueCount = firstBuffer.ValueCount;
        int componentCount = firstBuffer.ComponentCount;
        DataBuffer<float> result = new(meshes.Sum(mesh => select(mesh)!.ElementCount), valueCount, componentCount);
        int offset = 0;

        for (int meshIndex = 0; meshIndex < meshes.Count; meshIndex++)
        {
            DataBuffer source = select(meshes[meshIndex])!;

            for (int element = 0; element < source.ElementCount; element++)
            {
                for (int value = 0; value < valueCount; value++)
                {
                    Vector3 transformed = transform(source.GetVector3(element, value), toTarget[meshIndex]);

                    for (int component = 0; component < componentCount; component++)
                        result.Add(offset + element, value, component, component < 3 ? transformed[component] : source.Get<float>(element, value, component));
                }
            }

            offset += source.ElementCount;
        }

        return result;
    }

    private static DataBuffer? Concat(IReadOnlyList<Mesh> meshes, Func<Mesh, DataBuffer?> select)
    {
        if (select(meshes[0]) is not { } firstBuffer)
            return null;

        DataBuffer result = firstBuffer.Gather([.. Enumerable.Range(0, firstBuffer.ElementCount)]);
        int offset = firstBuffer.ElementCount;

        foreach (Mesh mesh in meshes.Skip(1))
        {
            DataBuffer source = select(mesh)!;

            for (int element = 0; element < source.ElementCount; element++)
            {
                for (int value = 0; value < source.ValueCount; value++)
                {
                    for (int component = 0; component < source.ComponentCount; component++)
                        result.Add(offset + element, value, component, source.Get<double>(element, value, component));
                }
            }

            offset += source.ElementCount;
        }

        return result;
    }

    private static DataBuffer CombineFaceIndices(IReadOnlyList<Mesh> meshes)
    {
        int[] indices = new int[meshes.Sum(mesh => mesh.FaceIndices!.ElementCount)];
        int indexOffset = 0;
        int vertexOffset = 0;

        foreach (Mesh mesh in meshes)
        {
            for (int i = 0; i < mesh.FaceIndices!.ElementCount; i++)
                indices[indexOffset + i] = vertexOffset + mesh.FaceIndices.Get<int>(i, 0, 0);

            indexOffset += mesh.FaceIndices.ElementCount;
            vertexOffset += mesh.VertexCount;
        }

        return new DataBuffer<int>(indices, 1, 1);
    }

    private static Skin CombineSkins(IReadOnlyList<Mesh> meshes, Matrix4x4[] toTarget)
    {
        List<SkeletonBone> bones = [];
        List<Matrix4x4> inverseBindMatrices = [];
        int influenceCount = meshes.Max(mesh => mesh.Skin!.InfluenceCount);
        DataBuffer<int> boneIndices = new(meshes.Sum(mesh => mesh.VertexCount), influenceCount, 1);
        DataBuffer<float> boneWeights = new(meshes.Sum(mesh => mesh.VertexCount), influenceCount, 1);
        int vertexOffset = 0;

        for (int meshIndex = 0; meshIndex < meshes.Count; meshIndex++)
        {
            Mesh mesh = meshes[meshIndex];
            Skin skin = mesh.Skin!;
            Matrix4x4 meshBindWorld = mesh.GetBindWorldMatrix();
            Matrix4x4 fromTarget = Matrix4x4.Invert(toTarget[meshIndex], out Matrix4x4 inverse) ? inverse : Matrix4x4.Identity;
            int[] boneMap = new int[skin.Bones.Count];

            for (int bone = 0; bone < skin.Bones.Count; bone++)
                boneMap[bone] = GetOrAddBone(bones, inverseBindMatrices, skin.Bones[bone], fromTarget * skin.GetInverseBindMatrix(bone, meshBindWorld));

            for (int vertex = 0; vertex < mesh.VertexCount; vertex++)
            {
                for (int influence = 0; influence < influenceCount; influence++)
                {
                    bool present = influence < skin.InfluenceCount;
                    boneIndices.Add(vertexOffset + vertex, influence, 0, present ? boneMap[skin.GetBoneIndex(vertex, influence)] : 0);
                    boneWeights.Add(vertexOffset + vertex, influence, 0, present ? skin.BoneWeights.Get<float>(vertex, influence, 0) : 0f);
                }
            }

            vertexOffset += mesh.VertexCount;
        }

        return new Skin(bones, boneIndices, boneWeights, inverseBindMatrices) { Name = meshes[0].Skin!.Name };
    }

    private static int GetOrAddBone(List<SkeletonBone> bones, List<Matrix4x4> inverseBindMatrices, SkeletonBone bone, Matrix4x4 inverseBindMatrix)
    {
        for (int i = 0; i < bones.Count; i++)
        {
            if (ReferenceEquals(bones[i], bone) && MatricesEqual(inverseBindMatrices[i], inverseBindMatrix))
                return i;
        }

        bones.Add(bone);
        inverseBindMatrices.Add(inverseBindMatrix);
        return bones.Count - 1;
    }

    private static bool MatricesEqual(Matrix4x4 first, Matrix4x4 second)
    {
        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                if (MathF.Abs(first[row, column] - second[row, column]) > 1e-5f)
                    return false;
            }
        }

        return true;
    }

    private static Vector3 TransformNormal(Vector3 normal, Matrix4x4 transform)
    {
        Matrix4x4 normalMatrix = Matrix4x4.Invert(transform, out Matrix4x4 inverse) ? Matrix4x4.Transpose(inverse) : transform;
        return TransformDirection(normal, normalMatrix);
    }

    private static Vector3 TransformDirection(Vector3 direction, Matrix4x4 transform)
    {
        Vector3 transformed = Vector3.TransformNormal(direction, transform);
        return transformed.LengthSquared() > 1e-12f ? Vector3.Normalize(transformed) : direction;
    }
}
