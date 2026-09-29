// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace RedFox.Audio;

/// <summary>
/// Reads RIFF WAVE files containing integer or 32-bit float PCM into 16-bit <see cref="AudioBuffer"/> instances.
/// </summary>
public static class WaveFile
{
    private const ushort FormatPcm = 1;
    private const ushort FormatIeeeFloat = 3;
    private const ushort FormatExtensible = 0xFFFE;

    /// <summary>
    /// Reads a WAVE file from a stream.
    /// </summary>
    /// <param name="stream">The stream positioned at the RIFF header.</param>
    /// <returns>The decoded 16-bit PCM buffer.</returns>
    public static AudioBuffer Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        return Read(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }

    /// <summary>
    /// Reads a WAVE file from memory.
    /// </summary>
    /// <param name="data">The complete file contents.</param>
    /// <returns>The decoded 16-bit PCM buffer.</returns>
    /// <exception cref="AudioException">Thrown when the data is not a supported WAVE file.</exception>
    public static AudioBuffer Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12 || !data[..4].SequenceEqual("RIFF"u8) || !data[8..12].SequenceEqual("WAVE"u8))
        {
            throw new AudioException("The data is not a RIFF WAVE file.");
        }

        ushort formatTag = 0;
        int channels = 0;
        int sampleRate = 0;
        int bitsPerSample = 0;
        ReadOnlySpan<byte> samples = default;

        int offset = 12;
        while (offset + 8 <= data.Length)
        {
            ReadOnlySpan<byte> chunkId = data.Slice(offset, 4);
            int chunkSize = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset + 4, 4));
            int chunkStart = offset + 8;
            int chunkLength = Math.Clamp(chunkSize, 0, data.Length - chunkStart);
            ReadOnlySpan<byte> chunk = data.Slice(chunkStart, chunkLength);

            if (chunkId.SequenceEqual("fmt "u8) && chunk.Length >= 16)
            {
                formatTag = BinaryPrimitives.ReadUInt16LittleEndian(chunk);
                channels = BinaryPrimitives.ReadUInt16LittleEndian(chunk[2..]);
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(chunk[4..]);
                bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(chunk[14..]);

                if (formatTag == FormatExtensible && chunk.Length >= 26)
                {
                    formatTag = BinaryPrimitives.ReadUInt16LittleEndian(chunk[24..]);
                }
            }
            else if (chunkId.SequenceEqual("data"u8))
            {
                samples = chunk;
            }

            offset = chunkStart + chunkLength + (chunkLength & 1);
        }

        if (channels <= 0 || sampleRate <= 0 || samples.IsEmpty)
        {
            throw new AudioException("The WAVE file has no usable format or data chunk.");
        }

        short[] pcm = (formatTag, bitsPerSample) switch
        {
            (FormatPcm, 8) => ConvertUnsigned8(samples),
            (FormatPcm, 16) => MemoryMarshal.Cast<byte, short>(samples[..(samples.Length & ~1)]).ToArray(),
            (FormatPcm, 24) => ConvertInteger(samples, 3),
            (FormatPcm, 32) => ConvertInteger(samples, 4),
            (FormatIeeeFloat, 32) => ConvertFloat32(samples),
            _ => throw new AudioException($"Unsupported WAVE format tag {formatTag} with {bitsPerSample} bits per sample."),
        };

        return new AudioBuffer
        {
            SampleRate = sampleRate,
            Channels = channels,
            Samples = pcm,
        };
    }

    private static short[] ConvertUnsigned8(ReadOnlySpan<byte> samples)
    {
        short[] pcm = new short[samples.Length];
        for (int index = 0; index < samples.Length; index++)
        {
            pcm[index] = (short)((samples[index] - 128) << 8);
        }

        return pcm;
    }

    private static short[] ConvertInteger(ReadOnlySpan<byte> samples, int bytesPerSample)
    {
        int count = samples.Length / bytesPerSample;
        short[] pcm = new short[count];
        for (int index = 0; index < count; index++)
        {
            int start = index * bytesPerSample;
            pcm[index] = (short)(samples[start + bytesPerSample - 1] << 8 | samples[start + bytesPerSample - 2]);
        }

        return pcm;
    }

    private static short[] ConvertFloat32(ReadOnlySpan<byte> samples)
    {
        ReadOnlySpan<float> values = MemoryMarshal.Cast<byte, float>(samples[..(samples.Length & ~3)]);
        short[] pcm = new short[values.Length];
        for (int index = 0; index < values.Length; index++)
        {
            pcm[index] = (short)(Math.Clamp(values[index], -1.0f, 1.0f) * short.MaxValue);
        }

        return pcm;
    }
}
