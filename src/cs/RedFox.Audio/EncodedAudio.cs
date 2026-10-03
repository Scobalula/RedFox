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
/// Holds encoded audio data with metadata required to decode it.
/// </summary>
public sealed class EncodedAudio
{
    /// <summary>
    /// Gets the codec used to encode the audio data.
    /// </summary>
    public required AudioCodec Codec { get; init; }

    /// <summary>
    /// Gets the format specification for the decoded audio (sample rate, channels, and layout).
    /// </summary>
    public required AudioFormat Format { get; init; }

    /// <summary>
    /// Gets the raw encoded audio data.
    /// </summary>
    public required ReadOnlyMemory<byte> Data { get; init; }

    /// <summary>
    /// Gets the total number of frames in the decoded audio, or a negative value if unknown.
    /// </summary>
    public long FrameCount { get; init; } = -1;

    /// <summary>
    /// Gets the number of valid bits per sample in the encoded format, or 0 if not applicable.
    /// </summary>
    public int BitsPerSample { get; init; }

    /// <summary>
    /// Gets the block alignment of the encoded data, or 0 if not applicable.
    /// </summary>
    public int BlockAlign { get; init; }

    /// <summary>
    /// Gets codec-specific setup or header data, or an empty memory if not applicable.
    /// </summary>
    public ReadOnlyMemory<byte> Setup { get; init; }

    /// <summary>
    /// Gets the packet table describing the location and size of each encoded packet within the data.
    /// </summary>
    public ReadOnlyMemory<AudioPacket> Packets { get; init; }

    /// <summary>
    /// Gets the bytes of the packet at the specified index.
    /// </summary>
    /// <param name="index">The packet index.</param>
    /// <returns>A span containing the bytes of the specified packet.</returns>
    public ReadOnlySpan<byte> GetPacket(int index)
    {
        AudioPacket packet = Packets.Span[index];
        return Data.Span.Slice(packet.Offset, packet.Length);
    }
}
