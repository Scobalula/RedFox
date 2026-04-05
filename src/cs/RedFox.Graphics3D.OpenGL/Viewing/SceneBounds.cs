using System.Numerics;
using System.Runtime.CompilerServices;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D.OpenGL.Viewing;

public static class SceneBounds
{
    private const float MinimumWeight = 1e-6f;
    private static readonly ConditionalWeakTable<Mesh, MeshBoundsCache> MeshBoundsCaches = new();

    public static bool TryGetBounds(Scene scene, out Vector3 center, out float radius)
    {
        bool hasBounds = TryGetBounds(scene, out SceneBoundsInfo bounds);
        center = bounds.Center;
        radius = bounds.Radius;
        return hasBounds;
    }

    public static bool TryGetBounds(Scene scene, out SceneBoundsInfo bounds)
        => TryGetBounds(scene, Matrix4x4.Identity, out bounds);

    public static bool TryGetBounds(Scene scene, Matrix4x4 sceneTransform, out SceneBoundsInfo bounds)
    {
        ArgumentNullException.ThrowIfNull(scene);

        BoundsAccumulator accumulator = default;

        foreach (Mesh mesh in scene.RootNode.EnumerateDescendants<Mesh>())
            IncludeMeshBounds(mesh, sceneTransform, ref accumulator);

        if (!accumulator.HasValue)
        {
            bounds = new SceneBoundsInfo(Vector3.Zero, Vector3.Zero, Vector3.Zero, 1.0f);
            return false;
        }

        Vector3 center = (accumulator.Min + accumulator.Max) * 0.5f;
        float radius = MathF.Max(0.5f * Vector3.Distance(accumulator.Min, accumulator.Max), 1.0f);
        bounds = new SceneBoundsInfo(accumulator.Min, accumulator.Max, center, radius);
        return true;
    }

    private static void IncludeMeshBounds(Mesh mesh, Matrix4x4 sceneTransform, ref BoundsAccumulator accumulator)
    {
        if (mesh.Positions is null || mesh.VertexCount == 0)
            return;

        MeshBoundsCache cache = MeshBoundsCaches.GetValue(mesh, CreateMeshBoundsCache);
        float[] morphWeights = [];
        bool hasActiveMorphTargets = cache.HasMorphTargets && TryResolveMorphWeights(mesh, out morphWeights);

        if (cache.RequiresExactEvaluation || hasActiveMorphTargets)
        {
            IncludeExactMeshBounds(mesh, sceneTransform, hasActiveMorphTargets ? morphWeights : null, ref accumulator);
            return;
        }

        if (cache.StaticLocalBounds is LocalBounds staticLocalBounds)
            TransformBounds(staticLocalBounds, mesh.GetActiveWorldMatrix(), sceneTransform, ref accumulator);

        if (cache.BoneBounds.Length == 0 || mesh.SkinnedBones is not { Count: > 0 } skinnedBones)
            return;

        foreach (SkinLocalBounds boneBounds in cache.BoneBounds)
        {
            if ((uint)boneBounds.SkinIndex >= (uint)skinnedBones.Count)
                continue;

            TransformBounds(boneBounds.Bounds, skinnedBones[boneBounds.SkinIndex].GetActiveWorldMatrix(), sceneTransform, ref accumulator);
        }
    }

    private static void IncludeExactMeshBounds(Mesh mesh, Matrix4x4 sceneTransform, float[]? morphWeights, ref BoundsAccumulator accumulator)
    {
        if (mesh.Positions is null)
            return;

        Matrix4x4 meshWorld = mesh.GetActiveWorldMatrix();
        Matrix4x4[] skinTransforms = [];
        int skinTransformCount = 0;

        if (mesh.HasSkinning && mesh.SkinnedBones is { Count: > 0 } skinnedBones)
        {
            skinTransforms = new Matrix4x4[skinnedBones.Count];
            skinTransformCount = mesh.CopySkinTransforms(skinTransforms);
        }

        for (int vertexIndex = 0; vertexIndex < mesh.Positions.ElementCount; vertexIndex++)
        {
            Vector3 localPosition = mesh.Positions.GetVector3(vertexIndex, 0);
            localPosition = ApplyMorphTargets(localPosition, mesh.DeltaPositions, vertexIndex, morphWeights);

            Vector3 worldPosition = ResolveWorldPosition(mesh, vertexIndex, localPosition, skinTransforms.AsSpan(0, skinTransformCount), meshWorld);
            accumulator.Include(Vector3.Transform(worldPosition, sceneTransform));
        }
    }

    private static Vector3 ResolveWorldPosition(Mesh mesh, int vertexIndex, Vector3 localPosition, ReadOnlySpan<Matrix4x4> skinTransforms, Matrix4x4 meshWorld)
    {
        if (skinTransforms.IsEmpty || mesh.BoneIndices is null || mesh.BoneWeights is null)
            return Vector3.Transform(localPosition, meshWorld);

        int influenceCount = Math.Min(mesh.BoneIndices.ValueCount, mesh.BoneWeights.ValueCount);
        if (influenceCount == 0)
            return Vector3.Transform(localPosition, meshWorld);

        Vector3 skinnedPosition = Vector3.Zero;
        float totalWeight = 0.0f;

        for (int influenceIndex = 0; influenceIndex < influenceCount; influenceIndex++)
        {
            float weight = mesh.BoneWeights.Get<float>(vertexIndex, influenceIndex, 0);
            if (weight <= MinimumWeight)
                continue;

            int skinIndex = mesh.BoneIndices.Get<int>(vertexIndex, influenceIndex, 0);
            if ((uint)skinIndex >= (uint)skinTransforms.Length)
                continue;

            skinnedPosition += Vector3.Transform(localPosition, skinTransforms[skinIndex]) * weight;
            totalWeight += weight;
        }

        return totalWeight > 0.0f
            ? skinnedPosition
            : Vector3.Transform(localPosition, meshWorld);
    }

    private static Vector3 ApplyMorphTargets(Vector3 localPosition, DataBuffer? deltaPositions, int vertexIndex, float[]? morphWeights)
    {
        if (deltaPositions is null || morphWeights is null || morphWeights.Length == 0)
            return localPosition;

        int targetCount = Math.Min(deltaPositions.ValueCount, morphWeights.Length);
        for (int targetIndex = 0; targetIndex < targetCount; targetIndex++)
        {
            float weight = morphWeights[targetIndex];
            if (MathF.Abs(weight) <= MinimumWeight)
                continue;

            localPosition += deltaPositions.GetVector3(vertexIndex, targetIndex) * weight;
        }

        return localPosition;
    }

    private static void TransformBounds(LocalBounds bounds, Matrix4x4 worldTransform, Matrix4x4 sceneTransform, ref BoundsAccumulator accumulator)
    {
        foreach (Vector3 corner in EnumerateCorners(bounds.Min, bounds.Max))
        {
            Vector3 worldPosition = Vector3.Transform(corner, worldTransform);
            accumulator.Include(Vector3.Transform(worldPosition, sceneTransform));
        }
    }

    private static MeshBoundsCache CreateMeshBoundsCache(Mesh mesh)
    {
        if (mesh.Positions is null || mesh.VertexCount == 0)
            return MeshBoundsCache.Empty;

        bool hasSkinning = mesh.HasSkinning
            && mesh.SkinnedBones is { Count: > 0 }
            && mesh.BoneIndices is not null
            && mesh.BoneWeights is not null;

        if (!hasSkinning)
        {
            BoundsAccumulator staticBounds = default;
            for (int vertexIndex = 0; vertexIndex < mesh.Positions.ElementCount; vertexIndex++)
                staticBounds.Include(mesh.Positions.GetVector3(vertexIndex, 0));

            return new MeshBoundsCache(staticBounds.ToLocalBounds(), [], false, mesh.HasMorphTargets);
        }

        mesh.EnsureInverseBindMatrices();
        if (mesh.InverseBindMatrices is not { Count: > 0 } inverseBindMatrices)
            return new MeshBoundsCache(default, [], true, mesh.HasMorphTargets);

        Dictionary<int, BoundsAccumulator> boneBounds = [];
        BoundsAccumulator staticLocalBounds = default;
        int influenceCount = Math.Min(mesh.BoneIndices!.ValueCount, mesh.BoneWeights!.ValueCount);

        for (int vertexIndex = 0; vertexIndex < mesh.Positions.ElementCount; vertexIndex++)
        {
            Vector3 localPosition = mesh.Positions.GetVector3(vertexIndex, 0);
            bool hasPositiveWeight = false;

            for (int influenceIndex = 0; influenceIndex < influenceCount; influenceIndex++)
            {
                float weight = mesh.BoneWeights.Get<float>(vertexIndex, influenceIndex, 0);
                if (weight <= MinimumWeight)
                    continue;

                int skinIndex = mesh.BoneIndices.Get<int>(vertexIndex, influenceIndex, 0);
                if ((uint)skinIndex >= (uint)inverseBindMatrices.Count)
                    return new MeshBoundsCache(default, [], true, mesh.HasMorphTargets);

                Vector3 transformed = Vector3.Transform(localPosition, inverseBindMatrices[skinIndex]);

                if (!boneBounds.TryGetValue(skinIndex, out BoundsAccumulator boneAccumulator))
                    boneAccumulator = default;

                boneAccumulator.Include(transformed);
                boneBounds[skinIndex] = boneAccumulator;
                hasPositiveWeight = true;
            }

            if (!hasPositiveWeight)
                staticLocalBounds.Include(localPosition);
        }

        SkinLocalBounds[] cachedBoneBounds = boneBounds
            .Where(static pair => pair.Value.HasValue)
            .OrderBy(static pair => pair.Key)
            .Select(static pair => new SkinLocalBounds(pair.Key, pair.Value.ToLocalBounds()!.Value))
            .ToArray();

        return new MeshBoundsCache(staticLocalBounds.ToLocalBounds(), cachedBoneBounds, false, mesh.HasMorphTargets);
    }

    private static bool TryResolveMorphWeights(Mesh mesh, out float[] weights)
    {
        if (!mesh.HasMorphTargets || mesh.MorphTargetCount == 0)
        {
            weights = [];
            return false;
        }

        weights = new float[mesh.MorphTargetCount];

        foreach (BlendShape blendShape in EnumerateBlendShapes(mesh))
        {
            if ((uint)blendShape.TargetIndex >= (uint)weights.Length)
                continue;

            weights[blendShape.TargetIndex] = blendShape.Weight;
        }

        return weights.Any(static weight => MathF.Abs(weight) > MinimumWeight);
    }

    private static IEnumerable<BlendShape> EnumerateBlendShapes(Mesh mesh)
    {
        foreach (BlendShape blendShape in mesh.EnumerateDescendants<BlendShape>())
            yield return blendShape;

        if (mesh.Scene is null)
            yield break;

        foreach (BlendShape blendShape in mesh.Scene.RootNode.EnumerateDescendants<BlendShape>())
        {
            if (blendShape.OwnerMesh == mesh && !blendShape.IsDescendantOf(mesh))
                yield return blendShape;
        }
    }

    private static IEnumerable<Vector3> EnumerateCorners(Vector3 min, Vector3 max)
    {
        yield return new Vector3(min.X, min.Y, min.Z);
        yield return new Vector3(min.X, min.Y, max.Z);
        yield return new Vector3(min.X, max.Y, min.Z);
        yield return new Vector3(min.X, max.Y, max.Z);
        yield return new Vector3(max.X, min.Y, min.Z);
        yield return new Vector3(max.X, min.Y, max.Z);
        yield return new Vector3(max.X, max.Y, min.Z);
        yield return new Vector3(max.X, max.Y, max.Z);
    }

    private sealed record MeshBoundsCache(LocalBounds? StaticLocalBounds, SkinLocalBounds[] BoneBounds, bool RequiresExactEvaluation, bool HasMorphTargets)
    {
        public static MeshBoundsCache Empty { get; } = new(default, [], false, false);
    }

    private readonly record struct SkinLocalBounds(int SkinIndex, LocalBounds Bounds);
    private readonly record struct LocalBounds(Vector3 Min, Vector3 Max);

    private struct BoundsAccumulator
    {
        public bool HasValue { get; private set; }
        public Vector3 Min { get; private set; }
        public Vector3 Max { get; private set; }

        public void Include(Vector3 position)
        {
            if (!HasValue)
            {
                Min = position;
                Max = position;
                HasValue = true;
                return;
            }

            Min = Vector3.Min(Min, position);
            Max = Vector3.Max(Max, position);
        }

        public LocalBounds? ToLocalBounds()
        {
            return HasValue ? new LocalBounds(Min, Max) : null;
        }
    }
}

public readonly record struct SceneBoundsInfo(Vector3 Min, Vector3 Max, Vector3 Center, float Radius);
