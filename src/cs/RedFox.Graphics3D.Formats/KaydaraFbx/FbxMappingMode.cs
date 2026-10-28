namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Identifies which mesh component an FBX layer element stores one value for.
/// </summary>
public enum FbxMappingMode
{
    /// <summary>
    /// One value per polygon corner.
    /// </summary>
    ByPolygonVertex,

    /// <summary>
    /// One value per control point.
    /// </summary>
    ByControlPoint,

    /// <summary>
    /// One value per polygon.
    /// </summary>
    ByPolygon,

    /// <summary>
    /// One value per edge.
    /// </summary>
    ByEdge,

    /// <summary>
    /// A single value shared by the whole mesh.
    /// </summary>
    AllSame,
}
