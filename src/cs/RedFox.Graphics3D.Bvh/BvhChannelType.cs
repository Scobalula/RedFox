namespace RedFox.Graphics3D.Bvh;

/// <summary>
/// Identifies one motion channel in a BVH hierarchy definition.
/// </summary>
public enum BvhChannelType
{
    /// <summary>
    /// A translation channel that animates the local X position.
    /// </summary>
    Xposition,

    /// <summary>
    /// A translation channel that animates the local Y position.
    /// </summary>
    Yposition,

    /// <summary>
    /// A translation channel that animates the local Z position.
    /// </summary>
    Zposition,

    /// <summary>
    /// A rotation channel that animates the local X rotation in degrees.
    /// </summary>
    Xrotation,

    /// <summary>
    /// A rotation channel that animates the local Y rotation in degrees.
    /// </summary>
    Yrotation,

    /// <summary>
    /// A rotation channel that animates the local Z rotation in degrees.
    /// </summary>
    Zrotation,
}
