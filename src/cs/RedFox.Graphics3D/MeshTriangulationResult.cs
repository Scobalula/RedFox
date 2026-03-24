namespace RedFox.Graphics3D;

/// <summary>
/// Stores triangulation output generated from polygon face topology.
/// </summary>
/// <param name="TriangleIndices">Triangle-list indices.</param>
/// <param name="TrianglesPerFace">Triangle count generated per source polygon face.</param>
public readonly record struct MeshTriangulationResult(int[] TriangleIndices, int[] TrianglesPerFace);
