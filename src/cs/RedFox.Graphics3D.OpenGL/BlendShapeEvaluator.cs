using System.Numerics;
using RedFox.Graphics3D.Buffers;

namespace RedFox.Graphics3D.OpenGL;

/// <summary>
/// Evaluates blend shapes and resolves morph target weights for mesh deformation.
/// Centralises logic shared between the renderer's dynamic mesh updates and scene bounds computation.
/// </summary>
internal static class BlendShapeEvaluator
{
    private const float MinimumWeight = 1e-6f;

    /// <summary>
    /// Resolves per-target morph weights for a mesh by enumerating all associated blend shapes.
    /// </summary>
    /// <param name="mesh">The mesh whose blend shapes to evaluate.</param>
    /// <param name="morphTargetCount">The number of morph targets declared on the mesh.</param>
    /// <returns>An array of weights indexed by morph target index.</returns>
    public static float[] ResolveMorphWeights(Mesh mesh, int morphTargetCount)
    {
        float[] result = new float[morphTargetCount];

        foreach (BlendShape blendShape in EnumerateBlendShapes(mesh))
        {
            if ((uint)blendShape.TargetIndex >= (uint)result.Length)
                continue;

            result[blendShape.TargetIndex] = blendShape.Weight;
        }

        return result;
    }

    /// <summary>
    /// Attempts to resolve morph weights and returns <c>true</c> if any non-zero weight is found.
    /// </summary>
    /// <param name="mesh">The mesh whose blend shapes to evaluate.</param>
    /// <param name="weights">The resolved weight array, or an empty array if the mesh has no morph targets.</param>
    /// <returns><c>true</c> if at least one morph target has a non-zero weight.</returns>
    public static bool TryResolveMorphWeights(Mesh mesh, out float[] weights)
    {
        if (!mesh.HasMorphTargets || mesh.MorphTargetCount == 0)
        {
            weights = [];
            return false;
        }

        weights = ResolveMorphWeights(mesh, mesh.MorphTargetCount);
        return weights.Any(static w => MathF.Abs(w) > MinimumWeight);
    }

    /// <summary>
    /// Enumerates all blend shapes that affect the specified mesh, including both
    /// descendants of the mesh and root-level blend shapes that reference it.
    /// </summary>
    public static IEnumerable<BlendShape> EnumerateBlendShapes(Mesh mesh)
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

    /// <summary>
    /// Applies morph target deltas to a flat float array using the given weights.
    /// Each vertex is displaced by the weighted sum of all morph target deltas.
    /// </summary>
    public static void ApplyMorphTargets(float[] destination, float[]? deltas, int vertexCount, int componentCount, ReadOnlySpan<float> weights)
    {
        if (deltas is null)
            return;

        for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
        {
            int vertexBase = vertexIndex * componentCount;

            for (int targetIndex = 0; targetIndex < weights.Length; targetIndex++)
            {
                float weight = weights[targetIndex];
                if (MathF.Abs(weight) < MinimumWeight)
                    continue;

                int deltaBase = ((vertexIndex * weights.Length) + targetIndex) * componentCount;
                for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
                    destination[vertexBase + componentIndex] += deltas[deltaBase + componentIndex] * weight;
            }
        }
    }

    /// <summary>
    /// Normalises packed N-component vectors in-place within a flat float array.
    /// </summary>
    public static void NormalizeVectors(float[] values, int componentCount)
    {
        for (int i = 0; i < values.Length; i += componentCount)
        {
            float lengthSquared = 0.0f;
            for (int c = 0; c < componentCount; c++)
                lengthSquared += values[i + c] * values[i + c];

            if (lengthSquared <= 1e-12f)
                continue;

            float inverseLength = 1.0f / MathF.Sqrt(lengthSquared);
            for (int c = 0; c < componentCount; c++)
                values[i + c] *= inverseLength;
        }
    }
}
