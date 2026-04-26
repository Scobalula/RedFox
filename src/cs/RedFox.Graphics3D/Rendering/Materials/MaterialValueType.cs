namespace RedFox.Graphics3D.Rendering.Materials;

/// <summary>
/// Identifies the value type required by a material uniform input.
/// </summary>
public enum MaterialValueType
{
    /// <summary>
    /// A 32-bit floating-point scalar.
    /// </summary>
    Float = 0,

    /// <summary>
    /// A two-component floating-point vector.
    /// </summary>
    Float2 = 1,

    /// <summary>
    /// A three-component floating-point vector.
    /// </summary>
    Float3 = 2,

    /// <summary>
    /// A four-component floating-point vector.
    /// </summary>
    Float4 = 3,

    /// <summary>
    /// A 4x4 floating-point matrix.
    /// </summary>
    Matrix4x4 = 4,

    /// <summary>
    /// A 32-bit signed integer scalar.
    /// </summary>
    Int32 = 5,

    /// <summary>
    /// A 32-bit unsigned integer scalar.
    /// </summary>
    UInt32 = 6,

    /// <summary>
    /// A Boolean value.
    /// </summary>
    Boolean = 7,
}