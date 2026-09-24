namespace RedFox.Imaging.BlockCompression;

/// <summary>
/// Describes a BC7 encoding mode's parameters, including subset count,
/// partition/rotation/index selection bit widths, channel precisions,
/// P-bit configuration, and index bit counts.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="BC7ModeDescriptor"/> struct.
/// </remarks>
/// <param name="numSubsets">Number of partition subsets (1, 2, or 3).</param>
/// <param name="partitionBits">Number of bits used to select a partition shape.</param>
/// <param name="rotationBits">Number of bits for the channel rotation selector.</param>
/// <param name="indexSelectionBits">Number of bits for the index selection flag.</param>
/// <param name="colorBits">Precision of color endpoint values per channel before P-bit expansion.</param>
/// <param name="alphaBits">Precision of alpha endpoint values, or 0 when alpha is absent.</param>
/// <param name="endpointPBits">1 if each endpoint has its own P-bit; otherwise 0.</param>
/// <param name="sharedPBits">1 if each subset pair shares a P-bit; otherwise 0.</param>
/// <param name="indexBits">Bit width for primary interpolation indices.</param>
/// <param name="secondaryIndexBits">Bit width for secondary interpolation indices, or 0 when unused.</param>
public readonly struct BC7ModeDescriptor(int numSubsets, int partitionBits, int rotationBits, int indexSelectionBits, int colorBits, int alphaBits, int endpointPBits, int sharedPBits, int indexBits, int secondaryIndexBits)
{

    /// <summary>
    /// Gets the number of subsets encoded by the mode.
    /// </summary>
    public int NumSubsets { get; } = numSubsets;

    /// <summary>
    /// Gets the number of bits used to select a partition shape.
    /// </summary>
    public int PartitionBits { get; } = partitionBits;

    /// <summary>
    /// Gets the number of bits used for channel rotation.
    /// </summary>
    public int RotationBits { get; } = rotationBits;

    /// <summary>
    /// Gets the number of bits used for the index-selection flag.
    /// </summary>
    public int IndexSelectionBits { get; } = indexSelectionBits;

    /// <summary>
    /// Gets the precision of color endpoint values per channel before P-bit expansion.
    /// </summary>
    public int ColorBits { get; } = colorBits;

    /// <summary>
    /// Gets the precision of alpha endpoint values.
    /// </summary>
    public int AlphaBits { get; } = alphaBits;

    /// <summary>
    /// Gets the number of endpoint P-bits assigned per endpoint.
    /// </summary>
    public int EndpointPBits { get; } = endpointPBits;

    /// <summary>
    /// Gets the number of shared P-bits assigned per subset pair.
    /// </summary>
    public int SharedPBits { get; } = sharedPBits;

    /// <summary>
    /// Gets the bit width used for primary interpolation indices.
    /// </summary>
    public int IndexBits { get; } = indexBits;

    /// <summary>
    /// Gets the bit width used for secondary interpolation indices.
    /// </summary>
    public int SecondaryIndexBits { get; } = secondaryIndexBits;
}
