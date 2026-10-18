namespace RedFox.Graphics3D.IO.SceneFile;

/// <summary>
/// Identifies the component types supported by scene file buffers.
/// </summary>
public enum SceneBufferType : byte
{
    /// <summary>
    /// Null buffer marker.
    /// </summary>
    None	= 0,
    /// <summary>
    /// 8-bit signed integer components.
    /// </summary>
    Int8	= 1,
    /// <summary>
    /// 8-bit unsigned integer components.
    /// </summary>
    UInt8	= 2,
    /// <summary>
    /// 16-bit signed integer components.
    /// </summary>
    Int16	= 3,
    /// <summary>
    /// 16-bit unsigned integer components.
    /// </summary>
    UInt16	= 4,
    /// <summary>
    /// 32-bit signed integer components.
    /// </summary>
    Int32	= 5,
    /// <summary>
    /// 32-bit unsigned integer components.
    /// </summary>
    UInt32	= 6,
    /// <summary>
    /// 64-bit signed integer components.
    /// </summary>
    Int64	= 7,
    /// <summary>
    /// 64-bit unsigned integer components.
    /// </summary>
    UInt64	= 8,
    /// <summary>
    /// 16-bit floating-point components.
    /// </summary>
    Half	= 9,
    /// <summary>
    /// 32-bit floating-point components.
    /// </summary>
    Single	= 10,
    /// <summary>
    /// 64-bit floating-point components.
    /// </summary>
    Double	= 11,
}
