namespace RedFox.Graphics2D.BC
{
    /// <summary>
    /// Describes a BC7 encoding mode's parameters, including subset count,
    /// partition/rotation/index selection bit widths, channel precisions,
    /// P-bit configuration, and index bit counts.
    /// </summary>
    /// <param name="NumSubsets">Number of partition subsets (1, 2, or 3).</param>
    /// <param name="PartitionBits">Number of bits used to select a partition shape.</param>
    /// <param name="RotationBits">Number of bits for the channel rotation selector.</param>
    /// <param name="IndexSelectionBits">Number of bits for the index selection flag (modes 4 only).</param>
    /// <param name="ColorBits">Precision of color endpoint values per channel (before P-bit).</param>
    /// <param name="AlphaBits">Precision of alpha endpoint values (0 if no alpha).</param>
    /// <param name="EndpointPBits">1 if each endpoint has its own P-bit, 0 otherwise.</param>
    /// <param name="SharedPBits">1 if each subset pair shares a P-bit, 0 otherwise.</param>
    /// <param name="IndexBits">Bit width for primary interpolation indices.</param>
    /// <param name="SecondaryIndexBits">Bit width for secondary indices (0 if single index set).</param>
    public readonly record struct BC7ModeDescriptor(
        int NumSubsets,
        int PartitionBits,
        int RotationBits,
        int IndexSelectionBits,
        int ColorBits,
        int AlphaBits,
        int EndpointPBits,
        int SharedPBits,
        int IndexBits,
        int SecondaryIndexBits);
}
