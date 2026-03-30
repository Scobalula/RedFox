namespace RedFox.Graphics3D.Gltf;

/// <summary>
/// Represents a node in the glTF scene graph. Nodes can carry a mesh, skin, camera,
/// transform (TRS or matrix), and child node references.
/// </summary>
public sealed class GltfNode
{
    /// <summary>
    /// Gets or sets the optional name of this node.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the indices of this node's children.
    /// </summary>
    public int[]? Children { get; set; }

    /// <summary>
    /// Gets or sets the index of the mesh attached to this node, or -1 if none.
    /// </summary>
    public int Mesh { get; set; } = -1;

    /// <summary>
    /// Gets or sets the index of the skin used for skeletal animation, or -1 if none.
    /// </summary>
    public int Skin { get; set; } = -1;

    /// <summary>
    /// Gets or sets the index of the camera attached to this node, or -1 if none.
    /// </summary>
    public int Camera { get; set; } = -1;

    /// <summary>
    /// Gets or sets the translation component (X, Y, Z). Defaults to origin.
    /// </summary>
    public float[]? Translation { get; set; }

    /// <summary>
    /// Gets or sets the rotation component as a quaternion (X, Y, Z, W). Defaults to identity.
    /// </summary>
    public float[]? Rotation { get; set; }

    /// <summary>
    /// Gets or sets the scale component (X, Y, Z). Defaults to unit scale.
    /// </summary>
    public float[]? Scale { get; set; }

    /// <summary>
    /// Gets or sets the 4x4 column-major transformation matrix.
    /// When present, overrides <see cref="Translation"/>, <see cref="Rotation"/>, and <see cref="Scale"/>.
    /// </summary>
    public float[]? Matrix { get; set; }

    /// <summary>
    /// Gets or sets the morph target weights for this node's mesh.
    /// </summary>
    public float[]? Weights { get; set; }
}
