namespace RedFox.Graphics3D.IO.SceneFile;

/// <summary>
/// Identifies the value types supported by scene file attributes.
/// </summary>
public enum SceneAttributeType : byte
{
    /// <summary>
    /// Boolean value.
    /// </summary>
    Bool		= 1,
    /// <summary>
    /// 32-bit signed integer value.
    /// </summary>
    Int32		= 2,
    /// <summary>
    /// 32-bit unsigned integer value.
    /// </summary>
    UInt32		= 3,
    /// <summary>
    /// 64-bit signed integer value.
    /// </summary>
    Int64		= 4,
    /// <summary>
    /// 64-bit unsigned integer value.
    /// </summary>
    UInt64		= 5,
    /// <summary>
    /// 128-bit unsigned integer value.
    /// </summary>
    UInt128		= 6,
    /// <summary>
    /// Single-precision floating-point value.
    /// </summary>
    Single		= 7,
    /// <summary>
    /// Double-precision floating-point value.
    /// </summary>
    Double		= 8,
    /// <summary>
    /// String value.
    /// </summary>
    String		= 9,
    /// <summary>
    /// Two-component vector value.
    /// </summary>
    Vector2		= 10,
    /// <summary>
    /// Three-component vector value.
    /// </summary>
    Vector3		= 11,
    /// <summary>
    /// Four-component vector value.
    /// </summary>
    Vector4		= 12,
    /// <summary>
    /// Quaternion value.
    /// </summary>
    Quaternion	= 13,
    /// <summary>
    /// 4 by 4 matrix value.
    /// </summary>
    Matrix4x4	= 14,
}
