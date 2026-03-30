namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Describes a single drawable surface within a <see cref="GltfMesh"/>,
/// including vertex attribute accessors, an optional index accessor, material reference,
/// and optional morph targets.
/// </summary>
public sealed class GltfMeshPrimitive
{
    /// <summary>
    /// Gets or sets the dictionary mapping attribute semantic names to accessor indices.
    /// Standard semantics include POSITION, NORMAL, TANGENT, TEXCOORD_0, COLOR_0, JOINTS_0, WEIGHTS_0.
    /// </summary>
    public Dictionary<string, int> Attributes { get; set; } = [];

    /// <summary>
    /// Gets or sets the index of the accessor for face indices, or -1 if not indexed.
    /// </summary>
    public int Indices { get; set; } = -1;

    /// <summary>
    /// Gets or sets the index of the material applied to this primitive, or -1 for default.
    /// </summary>
    public int Material { get; set; } = -1;

    /// <summary>
    /// Gets or sets the rendering mode (topology). Defaults to triangles (4).
    /// </summary>
    public int Mode { get; set; } = GltfConstants.ModeTriangles;

    /// <summary>
    /// Gets or sets the list of morph targets. Each target is a dictionary mapping
    /// attribute semantic names (e.g., POSITION, NORMAL) to accessor indices.
    /// </summary>
    public List<Dictionary<string, int>>? Targets { get; set; }
}
