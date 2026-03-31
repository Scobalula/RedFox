namespace RedFox.Graphics2D.BC
{
    /// <summary>
    /// Describes a BC6H encoding mode's parameters, including subset count,
    /// endpoint precision, delta bit widths, and index bit count.
    /// </summary>
    /// <param name="NumSubsets">Number of partition subsets (1 or 2).</param>
    /// <param name="Transformed">Whether delta endpoints are sign-extended and added to the base.</param>
    /// <param name="EndpointBits">Precision of the base endpoint values per channel.</param>
    /// <param name="DeltaBitsR">Bit width for the red channel delta values.</param>
    /// <param name="DeltaBitsG">Bit width for the green channel delta values.</param>
    /// <param name="DeltaBitsB">Bit width for the blue channel delta values.</param>
    /// <param name="IndexBits">Number of bits per interpolation index (3 or 4).</param>
    public readonly record struct BC6HModeDescriptor(
        int NumSubsets,
        bool Transformed,
        int EndpointBits,
        int DeltaBitsR,
        int DeltaBitsG,
        int DeltaBitsB,
        int IndexBits);
}
