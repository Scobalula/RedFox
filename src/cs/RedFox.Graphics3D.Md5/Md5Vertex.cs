using System.Numerics;

namespace RedFox.Graphics3D.Md5;

/// <summary>
/// Represents one parsed vertex entry from the <c>mesh</c> section of an MD5 mesh file.
/// </summary>
/// <remarks>
/// Each vertex references a contiguous range of <see cref="Md5Weight"/> entries.
/// The final vertex position is computed by accumulating the weighted contributions
/// of all referenced weights.
/// </remarks>
/// <param name="uv">The texture coordinates for this vertex.</param>
/// <param name="weightIndex">The index of the first weight in the weight array.</param>
/// <param name="weightCount">The number of weights influencing this vertex.</param>
public readonly struct Md5Vertex(Vector2 uv, int weightIndex, int weightCount)
{
    /// <summary>
    /// Gets the texture coordinates for this vertex.
    /// </summary>
    public Vector2 UV { get; } = uv;

    /// <summary>
    /// Gets the index of the first weight that influences this vertex.
    /// </summary>
    public int WeightIndex { get; } = weightIndex;

    /// <summary>
    /// Gets the number of weights that influence this vertex.
    /// </summary>
    public int WeightCount { get; } = weightCount;
}
