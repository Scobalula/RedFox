namespace RedFox.Graphics2D.BC
{
    /// <summary>
    /// Describes a BC6H encoding mode's parameters, including subset count,
    /// endpoint precision, delta bit widths, and index bit count.
    /// </summary>
    public readonly struct BC6HModeDescriptor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BC6HModeDescriptor"/> struct.
        /// </summary>
        /// <param name="numSubsets">Number of partition subsets (1 or 2).</param>
        /// <param name="transformed">Whether delta endpoints are sign-extended and added to the base.</param>
        /// <param name="endpointBits">Precision of the base endpoint values per channel.</param>
        /// <param name="deltaBitsR">Bit width for the red channel delta values.</param>
        /// <param name="deltaBitsG">Bit width for the green channel delta values.</param>
        /// <param name="deltaBitsB">Bit width for the blue channel delta values.</param>
        /// <param name="indexBits">Number of bits per interpolation index (3 or 4).</param>
        public BC6HModeDescriptor(int numSubsets, bool transformed, int endpointBits, int deltaBitsR, int deltaBitsG, int deltaBitsB, int indexBits)
        {
            NumSubsets = numSubsets;
            Transformed = transformed;
            EndpointBits = endpointBits;
            DeltaBitsR = deltaBitsR;
            DeltaBitsG = deltaBitsG;
            DeltaBitsB = deltaBitsB;
            IndexBits = indexBits;
        }

        /// <summary>
        /// Gets the number of partition subsets used by the mode.
        /// </summary>
        public int NumSubsets { get; }

        /// <summary>
        /// Gets a value indicating whether delta endpoints are sign-extended and applied relative to the base endpoint.
        /// </summary>
        public bool Transformed { get; }

        /// <summary>
        /// Gets the precision of the base endpoint values per channel.
        /// </summary>
        public int EndpointBits { get; }

        /// <summary>
        /// Gets the bit width used for red channel delta values.
        /// </summary>
        public int DeltaBitsR { get; }

        /// <summary>
        /// Gets the bit width used for green channel delta values.
        /// </summary>
        public int DeltaBitsG { get; }

        /// <summary>
        /// Gets the bit width used for blue channel delta values.
        /// </summary>
        public int DeltaBitsB { get; }

        /// <summary>
        /// Gets the number of bits per interpolation index.
        /// </summary>
        public int IndexBits { get; }
    }
}
