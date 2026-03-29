namespace RedFox.Graphics2D.Tiff
{
    /// <summary>
    /// Represents TIFF-native, interleaved sample data prepared for writing.
    /// </summary>
    public readonly record struct TiffEncodedPixelData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TiffEncodedPixelData"/> type.
        /// </summary>
        /// <param name="pixelData">The TIFF sample bytes in interleaved channel order.</param>
        /// <param name="photometric">The TIFF photometric interpretation value.</param>
        /// <param name="bitsPerSample">The bits-per-sample values for each channel.</param>
        /// <param name="samplesPerPixel">The number of samples stored for each pixel.</param>
        /// <param name="extraSamples">The TIFF extra-samples value, or <see langword="null"/> when no extra sample is present.</param>
        public TiffEncodedPixelData(byte[] pixelData, ushort photometric, ushort[] bitsPerSample, ushort samplesPerPixel, ushort? extraSamples)
        {
            ArgumentNullException.ThrowIfNull(pixelData);
            ArgumentNullException.ThrowIfNull(bitsPerSample);

            PixelData = pixelData;
            Photometric = photometric;
            BitsPerSample = bitsPerSample;
            SamplesPerPixel = samplesPerPixel;
            ExtraSamples = extraSamples;
        }

        /// <summary>
        /// Gets the TIFF sample bytes in interleaved channel order.
        /// </summary>
        public byte[] PixelData { get; }

        /// <summary>
        /// Gets the TIFF photometric interpretation value.
        /// </summary>
        public ushort Photometric { get; }

        /// <summary>
        /// Gets the bits-per-sample values for each channel.
        /// </summary>
        public ushort[] BitsPerSample { get; }

        /// <summary>
        /// Gets the number of samples stored for each pixel.
        /// </summary>
        public ushort SamplesPerPixel { get; }

        /// <summary>
        /// Gets the TIFF extra-samples value, or <see langword="null"/> when none is present.
        /// </summary>
        public ushort? ExtraSamples { get; }
    }
}