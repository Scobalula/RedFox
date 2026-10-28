namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Identifies how an FBX node inherits the transform of its parent.
/// </summary>
public enum FbxInheritType
{
    /// <summary>
    /// Parent rotation and scale are applied component-wise without shearing.
    /// </summary>
    RrSs,

    /// <summary>
    /// The parent matrix is applied in full, the standard matrix hierarchy.
    /// </summary>
    RSrs,

    /// <summary>
    /// The parent's local scale is ignored, as with Maya segment scale compensation.
    /// </summary>
    Rrs,
}
