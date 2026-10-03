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
/// Holds decoded audio samples and provides information about their format.
/// </summary>
public sealed class AudioBuffer
{
    /// <summary>
    /// Gets the format of the audio samples (sample rate, channels, and layout).
    /// </summary>
    public AudioFormat Format { get; }

    /// <summary>
    /// Gets the sample format specifying how samples are encoded in bytes.
    /// </summary>
    public SampleFormat SampleFormat { get; }

    /// <summary>
    /// Gets the raw audio sample data as bytes.
    /// </summary>
    public Memory<byte> Data { get; }

    /// <summary>
    /// Gets the number of valid bits in each sample (may be less than the full sample format's bit depth).
    /// </summary>
    public int ValidBitsPerSample { get; }

    /// <summary>
    /// Gets the number of audio frames (samples per channel).
    /// </summary>
    public int FrameCount { get; }

    /// <summary>
    /// Gets the total number of samples across all channels.
    /// </summary>
    public int SampleCount => FrameCount * Format.Channels;

    /// <summary>
    /// Gets the number of bytes used by a single audio frame across all channels.
    /// </summary>
    public int BytesPerFrame => SampleFormatInfo.GetBytesPerSample(SampleFormat) * Format.Channels;

    /// <summary>
    /// Gets the total duration of the audio buffer.
    /// </summary>
    public TimeSpan Duration => TimeSpan.FromSeconds((double)FrameCount / Format.SampleRate);

    /// <summary>
    /// Initializes a new instance of the AudioBuffer class with the specified format and data,
    /// assuming the valid bits per sample matches the full sample format bit depth.
    /// </summary>
    /// <param name="format">The audio format describing sample rate and channels.</param>
    /// <param name="sampleFormat">The format in which samples are encoded.</param>
    /// <param name="data">The raw audio sample data.</param>
    public AudioBuffer(AudioFormat format, SampleFormat sampleFormat, Memory<byte> data) : this(format, sampleFormat, data, SampleFormatInfo.GetBitsPerSample(sampleFormat))
    {
    }

    /// <summary>
    /// Initializes a new instance of the AudioBuffer class with the specified format, data, and valid bit depth.
    /// </summary>
    /// <param name="format">The audio format describing sample rate and channels.</param>
    /// <param name="sampleFormat">The format in which samples are encoded.</param>
    /// <param name="data">The raw audio sample data.</param>
    /// <param name="validBitsPerSample">The number of valid bits in each sample.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the format has invalid parameters or when valid bits exceed the sample format's maximum.
    /// </exception>
    public AudioBuffer(AudioFormat format, SampleFormat sampleFormat, Memory<byte> data, int validBitsPerSample)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(format.SampleRate, nameof(format));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(format.Channels, nameof(format));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(validBitsPerSample);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(validBitsPerSample, SampleFormatInfo.GetBitsPerSample(sampleFormat));

        Format = format;
        SampleFormat = sampleFormat;
        ValidBitsPerSample = validBitsPerSample;
        FrameCount = data.Length / BytesPerFrame;
        Data = data[..(FrameCount * BytesPerFrame)];
    }

    /// <summary>
    /// Initializes a new instance of the AudioBuffer class with the specified format and frame count,
    /// allocating new uninitialized sample data.
    /// </summary>
    /// <param name="format">The audio format describing sample rate and channels.</param>
    /// <param name="sampleFormat">The format in which samples are encoded.</param>
    /// <param name="frameCount">The number of audio frames to allocate.</param>
    public AudioBuffer(AudioFormat format, SampleFormat sampleFormat, int frameCount) : this(format, sampleFormat, new byte[frameCount * format.Channels * SampleFormatInfo.GetBytesPerSample(sampleFormat)])
    {
    }

    /// <summary>
    /// Gets the audio samples cast to the specified unmanaged type.
    /// </summary>
    /// <typeparam name="T">The unmanaged type to cast samples to.</typeparam>
    /// <returns>A span of samples cast to the specified type.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the specified type does not match the buffer's sample format.
    /// </exception>
    public Span<T> GetSamples<T>() where T : unmanaged
    {
        if (!IsSampleType<T>())
            throw new InvalidOperationException($"{typeof(T).Name} does not match the {SampleFormat} sample format.");

        return MemoryMarshal.Cast<byte, T>(Data.Span);
    }

    /// <summary>
    /// Gets a span of raw bytes for the specified range of audio frames.
    /// </summary>
    /// <param name="start">The starting frame index.</param>
    /// <param name="count">The number of frames to retrieve.</param>
    /// <returns>A span of bytes containing the requested frames.</returns>
    public Span<byte> GetFrames(int start, int count) => Data.Span.Slice(start * BytesPerFrame, count * BytesPerFrame);

    /// <summary>
    /// Creates a new AudioBuffer with samples converted to the specified sample format.
    /// </summary>
    /// <param name="sampleFormat">The target sample format.</param>
    /// <returns>
    /// If the target format matches the current format, returns this instance unchanged;
    /// otherwise, returns a new buffer with converted samples.
    /// </returns>
    public AudioBuffer Convert(SampleFormat sampleFormat)
    {
        if (sampleFormat == SampleFormat)
            return this;

        byte[] data = new byte[FrameCount * Format.Channels * SampleFormatInfo.GetBytesPerSample(sampleFormat)];
        SampleConverter.Convert(Data.Span, SampleFormat, data, sampleFormat);

        return new AudioBuffer(Format, sampleFormat, data, SampleFormatInfo.GetConvertedValidBits(ValidBitsPerSample, sampleFormat));
    }

    private bool IsSampleType<T>() => SampleFormat switch
    {
        SampleFormat.UInt8 => typeof(T) == typeof(byte),
        SampleFormat.Int16 => typeof(T) == typeof(short),
        SampleFormat.Int32 => typeof(T) == typeof(int),
        SampleFormat.Float32 => typeof(T) == typeof(float),
        SampleFormat.Float64 => typeof(T) == typeof(double),
        _ => false,
    };
}
