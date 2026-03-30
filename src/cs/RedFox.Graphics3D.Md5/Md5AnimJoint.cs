namespace RedFox.Graphics3D.Md5;

/// <summary>
/// Represents one parsed joint entry from the <c>hierarchy</c> section of an MD5 animation file.
/// </summary>
/// <remarks>
/// The <see cref="Flags"/> bit mask selects which transform components are stored in
/// the per-frame data array starting at <see cref="StartIndex"/>:
/// <list type="table">
///   <listheader><term>Bit</term><description>Component</description></listheader>
///   <item><term>0 (1)</term><description>Translate X</description></item>
///   <item><term>1 (2)</term><description>Translate Y</description></item>
///   <item><term>2 (4)</term><description>Translate Z</description></item>
///   <item><term>3 (8)</term><description>Quaternion X</description></item>
///   <item><term>4 (16)</term><description>Quaternion Y</description></item>
///   <item><term>5 (32)</term><description>Quaternion Z</description></item>
/// </list>
/// </remarks>
/// <param name="name">The name of the joint.</param>
/// <param name="parentIndex">
/// The index of the parent joint, or <c>-1</c> for root joints.
/// </param>
/// <param name="flags">A bit mask selecting which components are animated.</param>
/// <param name="startIndex">
/// The starting index into the per-frame component data array.
/// </param>
public readonly struct Md5AnimJoint(string name, int parentIndex, int flags, int startIndex)
{
    /// <summary>
    /// Gets the name of the joint.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the index of the parent joint, or <c>-1</c> for root joints.
    /// </summary>
    public int ParentIndex { get; } = parentIndex;

    /// <summary>
    /// Gets the bit mask selecting which transform components are animated.
    /// </summary>
    public int Flags { get; } = flags;

    /// <summary>
    /// Gets the starting index into the per-frame component data array.
    /// </summary>
    public int StartIndex { get; } = startIndex;
}
