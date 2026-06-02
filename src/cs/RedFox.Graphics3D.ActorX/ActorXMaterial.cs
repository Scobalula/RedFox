namespace RedFox.Graphics3D.ActorX;

/// <summary>
/// An ActorX material record. The name typically corresponds to a texture or shader slot in the source asset.
/// </summary>
/// <param name="Name">The material name.</param>
/// <param name="TextureIndex">The legacy texture index.</param>
/// <param name="PolyFlags">Polygon flags applied to faces using this material.</param>
/// <param name="AuxMaterial">An auxiliary material index, unused by most tools.</param>
/// <param name="AuxFlags">Auxiliary flags, unused by most tools.</param>
/// <param name="LodBias">The level-of-detail bias.</param>
/// <param name="LodStyle">The level-of-detail style.</param>
public readonly record struct ActorXMaterial(
    string Name,
    int TextureIndex,
    uint PolyFlags,
    int AuxMaterial,
    uint AuxFlags,
    int LodBias,
    int LodStyle);
