namespace RedFox.Graphics3D.Rendering;

/// <summary>
/// Identifies the comparison function used by depth or stencil testing.
/// </summary>
public enum CompareFunc
{
    /// <summary>
    /// The comparison never passes.
    /// </summary>
    Never = 0,

    /// <summary>
    /// The comparison passes when the incoming value is less than the stored value.
    /// </summary>
    Less = 1,

    /// <summary>
    /// The comparison passes when the incoming value is less than or equal to the stored value.
    /// </summary>
    LessOrEqual = 2,

    /// <summary>
    /// The comparison passes when the values are equal.
    /// </summary>
    Equal = 3,

    /// <summary>
    /// The comparison passes when the incoming value is greater than the stored value.
    /// </summary>
    Greater = 4,

    /// <summary>
    /// The comparison passes when the incoming value is greater than or equal to the stored value.
    /// </summary>
    GreaterOrEqual = 5,

    /// <summary>
    /// The comparison passes when the values are not equal.
    /// </summary>
    NotEqual = 6,

    /// <summary>
    /// The comparison always passes.
    /// </summary>
    Always = 7,
}