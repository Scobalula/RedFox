// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Audio;

/// <summary>
/// Provides utility methods for querying properties of sample formats.
/// </summary>
public static class SampleFormatInfo
{
    /// <summary>
    /// Gets the number of bytes required to store a single sample in the specified format.
    /// </summary>
    /// <param name="format">The sample format.</param>
    /// <returns>The number of bytes per sample.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the format is not recognized.</exception>
    public static int GetBytesPerSample(SampleFormat format) => format switch
    {
        SampleFormat.UInt8 => 1,
        SampleFormat.Int16 => 2,
        SampleFormat.Int24 => 3,
        SampleFormat.Int32 => 4,
        SampleFormat.Float32 => 4,
        SampleFormat.Float64 => 8,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>
    /// Gets the number of bits required to store a single sample in the specified format.
    /// </summary>
    /// <param name="format">The sample format.</param>
    /// <returns>The number of bits per sample.</returns>
    public static int GetBitsPerSample(SampleFormat format) => GetBytesPerSample(format) * 8;

    /// <summary>
    /// Gets a value indicating whether the specified format uses floating-point representation.
    /// </summary>
    /// <param name="format">The sample format.</param>
    /// <returns><see langword="true"/> for <see cref="SampleFormat.Float32"/> or <see cref="SampleFormat.Float64"/>; otherwise <see langword="false"/>.</returns>
    public static bool IsFloat(SampleFormat format) => format is SampleFormat.Float32 or SampleFormat.Float64;

    /// <summary>
    /// Gets the valid bits per sample after conversion to the specified format.
    /// </summary>
    /// <param name="validBitsPerSample">The original number of valid bits per sample.</param>
    /// <param name="format">The target sample format.</param>
    /// <returns>The number of valid bits in the converted format.</returns>
    public static int GetConvertedValidBits(int validBitsPerSample, SampleFormat format) => IsFloat(format) ? GetBitsPerSample(format) : Math.Min(validBitsPerSample, GetBitsPerSample(format));
}
