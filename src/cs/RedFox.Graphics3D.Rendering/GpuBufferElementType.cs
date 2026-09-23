namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Identifies the scalar or packed storage type carried by a GPU buffer payload.
/// </summary>
public enum GpuBufferElementType
{
    /// <summary>
    /// The buffer element type is not specified.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 16-bit floating-point elements.
    /// </summary>
    Float16 = 1,

    /// <summary>
    /// 32-bit floating-point elements.
    /// </summary>
    Float32 = 2,

    /// <summary>
    /// 64-bit floating-point elements.
    /// </summary>
    Float64 = 3,

    /// <summary>
    /// 8-bit signed integer elements.
    /// </summary>
    Int8 = 4,

    /// <summary>
    /// 8-bit unsigned integer elements.
    /// </summary>
    UInt8 = 5,

    /// <summary>
    /// 16-bit signed integer elements.
    /// </summary>
    Int16 = 6,

    /// <summary>
    /// 16-bit unsigned integer elements.
    /// </summary>
    UInt16 = 7,

    /// <summary>
    /// 32-bit signed integer elements.
    /// </summary>
    Int32 = 8,

    /// <summary>
    /// 32-bit unsigned integer elements.
    /// </summary>
    UInt32 = 9,

    /// <summary>
    /// 64-bit signed integer elements.
    /// </summary>
    Int64 = 10,

    /// <summary>
    /// 64-bit unsigned integer elements.
    /// </summary>
    UInt64 = 11,

    /// <summary>
    /// Packed 10-10-10-2 unsigned integer elements.
    /// </summary>
    PackedUInt1010102 = 12,

    /// <summary>
    /// Packed 10-10-10-2 signed integer elements.
    /// </summary>
    PackedSInt1010102 = 13,

    /// <summary>
    /// Packed 10-10-10-2 unsigned normalized elements.
    /// </summary>
    PackedUNorm1010102 = 14,

    /// <summary>
    /// Packed 10-10-10-2 signed normalized elements.
    /// </summary>
    PackedSNorm1010102 = 15,
}
