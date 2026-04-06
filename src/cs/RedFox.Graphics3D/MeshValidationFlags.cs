namespace RedFox.Graphics3D;

/// <summary>
/// Flags that control which structural checks are performed during mesh validation.
/// Multiple flags may be combined with the bitwise OR operator.
/// </summary>
[Flags]
public enum MeshValidationFlags
{
    /// <summary>No additional checks are performed; only index range validation is always active.</summary>
    None = 0x0,

    /// <summary>
    /// Checks for two triangles that share the same neighbour, which often indicates a back-facing
    /// duplicate. Requires adjacency data.
    /// </summary>
    BackFacing = 0x1,

    /// <summary>
    /// Checks for a single vertex that serves as the apex of two disconnected triangle fans (a bowtie).
    /// Requires adjacency data.
    /// </summary>
    Bowties = 0x2,

    /// <summary>Checks for degenerate triangles where two or more corner indices are identical.</summary>
    Degenerate = 0x4,

    /// <summary>
    /// Checks for unused triangles whose corners contain the sentinel <c>-1</c> / <c>0xFFFF</c> value.
    /// </summary>
    Unused = 0x8,

    /// <summary>
    /// Checks that every recorded neighbour pair refers back to the originating face (symmetric).
    /// Requires adjacency data.
    /// </summary>
    AsymmetricAdjacency = 0x10,
}
