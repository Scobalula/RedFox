namespace RedFox.Graphics3D.ActorX;

/// <summary>
/// An ActorX triangle, referencing three wedges and carrying material and smoothing metadata.
/// </summary>
/// <param name="Wedge0">The first wedge index.</param>
/// <param name="Wedge1">The second wedge index.</param>
/// <param name="Wedge2">The third wedge index.</param>
/// <param name="MaterialIndex">The index of the material assigned to this face.</param>
/// <param name="AuxMaterialIndex">An auxiliary material index, unused by most tools.</param>
/// <param name="SmoothingGroups">The smoothing-group bit mask for this face.</param>
public readonly record struct ActorXTriangle(
    int Wedge0,
    int Wedge1,
    int Wedge2,
    byte MaterialIndex,
    byte AuxMaterialIndex,
    uint SmoothingGroups);
