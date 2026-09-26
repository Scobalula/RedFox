namespace RedFox.Graphics3D.Formats.GLTransmissionFormat;

/// <summary>
/// Represents a glTF mesh, consisting of one or more <see cref="GltfMeshPrimitive"/>
/// instances that define the geometry and associated material.
/// </summary>
public sealed class GltfMesh
{
    /// <summary>
    /// Gets or sets the list of primitives that compose this mesh.
    /// </summary>
    public List<GltfMeshPrimitive> Primitives { get; set; } = [];

    /// <summary>
    /// Gets or sets the optional name of the mesh.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the morph target weights for this mesh.
    /// </summary>
    public float[]? Weights { get; set; }

    /// <summary>
    /// Gets or sets the morph target names stored in the mesh's <c>extras.targetNames</c>.
    /// </summary>
    public string[]? TargetNames { get; set; }
}
