namespace RedFox.Graphics3D;

/// <summary>
/// Specifies the weighting strategy used when computing per-vertex normals from face normals.
/// </summary>
public enum NormalGenerationMode
{
    /// <summary>
    /// Weights each face's normal contribution by the interior angle of the triangle at the shared vertex.
    /// Produces smooth, visually accurate normals on most meshes. This is the default mode.
    /// </summary>
    WeightedByAngle,

    /// <summary>
    /// Weights each face's normal contribution by the surface area of the triangle.
    /// Larger triangles have proportionally more influence on the vertex normal.
    /// </summary>
    WeightedByArea,

    /// <summary>
    /// Assigns equal weight to every adjacent face regardless of shape or size.
    /// </summary>
    EqualWeight,
}
