namespace RedFox.Graphics3D.Rendering.Backend;

/// <summary>
/// Identifies the storage type of a vertex attribute component.
/// </summary>
public enum VertexAttributeType
{
    /// <summary>
    /// 32-bit floating-point components.
    /// </summary>
    Float32 = 0,

    /// <summary>
    /// 16-bit floating-point components.
    /// </summary>
    Float16 = 1,

    /// <summary>
    /// 32-bit signed integer components.
    /// </summary>
    Int32 = 2,

    /// <summary>
    /// 32-bit unsigned integer components.
    /// </summary>
    UInt32 = 3,

    /// <summary>
    /// 16-bit signed integer components.
    /// </summary>
    Int16 = 4,

    /// <summary>
    /// 16-bit unsigned integer components.
    /// </summary>
    UInt16 = 5,

    /// <summary>
    /// 8-bit signed integer components.
    /// </summary>
    Int8 = 6,

    /// <summary>
    /// 8-bit unsigned integer components.
    /// </summary>
    UInt8 = 7,
}