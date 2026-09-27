using System;

namespace RedFox.Imaging.Formats.Tiff;

/// <summary>
/// Represents TIFF-native, interleaved sample data prepared for writing.
/// </summary>
/// <param name="pixelData">The TIFF sample bytes in interleaved channel order.</param>
/// <param name="photometric">The TIFF photometric interpretation value.</param>
/// <param name="bitsPerSample">The bits-per-sample values for each channel.</param>
/// <param name="samplesPerPixel">The number of samples stored for each pixel.</param>
/// <param name="extraSamples">The TIFF extra-samples value, or <see langword="null"/> when no extra sample is present.</param>
internal readonly record struct TiffEncodedPixelData(byte[] pixelData, ushort photometric, ushort[] bitsPerSample, ushort samplesPerPixel, ushort? extraSamples)
{
    /// <summary>
    /// Gets the TIFF sample bytes in interleaved channel order.
    /// </summary>
    public byte[] PixelData { get; } = pixelData ?? throw new ArgumentNullException(nameof(pixelData));

    /// <summary>
    /// Gets the TIFF photometric interpretation value.
    /// </summary>
    public ushort Photometric { get; } = photometric;

    /// <summary>
    /// Gets the bits-per-sample values for each channel.
    /// </summary>
    public ushort[] BitsPerSample { get; } = bitsPerSample ?? throw new ArgumentNullException(nameof(bitsPerSample));

    /// <summary>
    /// Gets the number of samples stored for each pixel.
    /// </summary>
    public ushort SamplesPerPixel { get; } = samplesPerPixel;

    /// <summary>
    /// Gets the TIFF extra-samples value, or <see langword="null"/> when none is present.
    /// </summary>
    public ushort? ExtraSamples { get; } = extraSamples;
}
