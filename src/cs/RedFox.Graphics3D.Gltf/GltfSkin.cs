namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Describes the skeletal skinning information for a mesh, including the joint hierarchy
/// and optional inverse bind matrices.
/// </summary>
public sealed class GltfSkin
{
    /// <summary>
    /// Gets or sets the optional name of this skin.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the index of the accessor containing the inverse bind matrices.
    /// A value of -1 indicates no explicit inverse bind matrices (identity assumed).
    /// </summary>
    public int InverseBindMatrices { get; set; } = -1;

    /// <summary>
    /// Gets or sets the index of the skeleton root node, or -1 if not specified.
    /// </summary>
    public int SkeletonRoot { get; set; } = -1;

    /// <summary>
    /// Gets or sets the array of node indices used as joints.
    /// </summary>
    public int[] Joints { get; set; } = [];
}
