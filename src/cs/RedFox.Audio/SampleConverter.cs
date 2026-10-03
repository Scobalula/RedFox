// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace RedFox.Audio;

/// <summary>
/// Utility for converting audio samples between different sample formats.
/// </summary>
public static class SampleConverter
{
    private const int ChunkSize = 1024;

    /// <summary>
    /// Converts audio samples from one sample format to another.
    /// </summary>
    /// <param name="source">The source audio samples.</param>
    /// <param name="sourceFormat">The format of the source samples.</param>
    /// <param name="destination">The buffer to fill with converted samples.</param>
    /// <param name="destinationFormat">The target format for the converted samples.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the destination buffer is too small to hold the converted samples.
    /// </exception>
    public static void Convert(ReadOnlySpan<byte> source, SampleFormat sourceFormat, Span<byte> destination, SampleFormat destinationFormat)
    {
        int sourceSize = SampleFormatInfo.GetBytesPerSample(sourceFormat);
        int destinationSize = SampleFormatInfo.GetBytesPerSample(destinationFormat);
        int sampleCount = source.Length / sourceSize;

        if (destination.Length < sampleCount * destinationSize)
            throw new ArgumentException("The destination is too small for the converted samples.", nameof(destination));

        if (sourceFormat == destinationFormat)
        {
            source[..(sampleCount * sourceSize)].CopyTo(destination);
            return;
        }

        Span<double> chunk = stackalloc double[ChunkSize];

        for (int offset = 0; offset < sampleCount; offset += ChunkSize)
        {
            int count = Math.Min(ChunkSize, sampleCount - offset);

            Read(source.Slice(offset * sourceSize, count * sourceSize), sourceFormat, chunk[..count]);
            Write(chunk[..count], destination.Slice(offset * destinationSize, count * destinationSize), destinationFormat);
        }
    }

    private static void Read(ReadOnlySpan<byte> source, SampleFormat format, Span<double> destination)
    {
        switch (format)
        {
            case SampleFormat.UInt8:
                for (int i = 0; i < destination.Length; i++)
                    destination[i] = (source[i] - 128) / 128.0;
                break;
            case SampleFormat.Int16:
                ReadOnlySpan<short> int16 = MemoryMarshal.Cast<byte, short>(source);
                for (int i = 0; i < destination.Length; i++)
                    destination[i] = int16[i] / 32768.0;
                break;
            case SampleFormat.Int24:
                for (int i = 0; i < destination.Length; i++)
                    destination[i] = (source[i * 3] | source[(i * 3) + 1] << 8 | (sbyte)source[(i * 3) + 2] << 16) / 8388608.0;
                break;
            case SampleFormat.Int32:
                ReadOnlySpan<int> int32 = MemoryMarshal.Cast<byte, int>(source);
                for (int i = 0; i < destination.Length; i++)
                    destination[i] = int32[i] / 2147483648.0;
                break;
            case SampleFormat.Float32:
                ReadOnlySpan<float> float32 = MemoryMarshal.Cast<byte, float>(source);
                for (int i = 0; i < destination.Length; i++)
                    destination[i] = float32[i];
                break;
            case SampleFormat.Float64:
                MemoryMarshal.Cast<byte, double>(source).CopyTo(destination);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    private static void Write(ReadOnlySpan<double> source, Span<byte> destination, SampleFormat format)
    {
        switch (format)
        {
            case SampleFormat.UInt8:
                for (int i = 0; i < source.Length; i++)
                    destination[i] = (byte)(Quantize(source[i], 128.0, sbyte.MinValue, sbyte.MaxValue) + 128);
                break;
            case SampleFormat.Int16:
                Span<short> int16 = MemoryMarshal.Cast<byte, short>(destination);
                for (int i = 0; i < source.Length; i++)
                    int16[i] = (short)Quantize(source[i], 32768.0, short.MinValue, short.MaxValue);
                break;
            case SampleFormat.Int24:
                for (int i = 0; i < source.Length; i++)
                {
                    int value = (int)Quantize(source[i], 8388608.0, -8388608, 8388607);

                    destination[i * 3] = (byte)value;
                    destination[(i * 3) + 1] = (byte)(value >> 8);
                    destination[(i * 3) + 2] = (byte)(value >> 16);
                }
                break;
            case SampleFormat.Int32:
                Span<int> int32 = MemoryMarshal.Cast<byte, int>(destination);
                for (int i = 0; i < source.Length; i++)
                    int32[i] = (int)Quantize(source[i], 2147483648.0, int.MinValue, int.MaxValue);
                break;
            case SampleFormat.Float32:
                Span<float> float32 = MemoryMarshal.Cast<byte, float>(destination);
                for (int i = 0; i < source.Length; i++)
                    float32[i] = (float)source[i];
                break;
            case SampleFormat.Float64:
                source.CopyTo(MemoryMarshal.Cast<byte, double>(destination));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }

    private static double Quantize(double value, double scale, double minimum, double maximum) => Math.Clamp(Math.Round(value * scale), minimum, maximum);
}
