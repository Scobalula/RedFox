namespace RedFox.Graphics3D.Formats.KaydaraFbx;

/// <summary>
/// Identifies the Euler rotation order stored in the FBX <c>RotationOrder</c> property, named by the order axes are applied.
/// </summary>
public enum FbxRotationOrder
{
    /// <summary>
    /// X is applied first, then Y, then Z.
    /// </summary>
    EulerXYZ,

    /// <summary>
    /// X is applied first, then Z, then Y.
    /// </summary>
    EulerXZY,

    /// <summary>
    /// Y is applied first, then Z, then X.
    /// </summary>
    EulerYZX,

    /// <summary>
    /// Y is applied first, then X, then Z.
    /// </summary>
    EulerYXZ,

    /// <summary>
    /// Z is applied first, then X, then Y.
    /// </summary>
    EulerZXY,

    /// <summary>
    /// Z is applied first, then Y, then X.
    /// </summary>
    EulerZYX,

    /// <summary>
    /// Spherical XYZ, evaluated as <see cref="EulerXYZ"/>.
    /// </summary>
    SphericXYZ,
}
