// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Buffers.Binary;

namespace RedFox.Audio.Opus;

/// <summary>
/// Represents the metadata from an Opus OpusHead header.
/// </summary>
public sealed class OpusHeader
{
    private const int FixedSize = 19;

    private static readonly (int Streams, int CoupledStreams, byte[] Mapping)[] VorbisLayouts =
    [
        (1, 0, [0]),
        (1, 1, [0, 1]),
        (2, 1, [0, 2, 1]),
        (2, 2, [0, 1, 2, 3]),
        (3, 2, [0, 4, 1, 2, 3]),
        (4, 2, [0, 4, 1, 2, 3, 5]),
        (5, 2, [0, 4, 1, 2, 3, 5, 6]),
        (5, 3, [0, 6, 1, 2, 3, 4, 5, 7]),
    ];

    private static ReadOnlySpan<byte> Magic => "OpusHead"u8;

    /// <summary>
    /// Gets the number of audio channels.
    /// </summary>
    public required int Channels { get; init; }

    /// <summary>
    /// Gets the number of samples to skip at the beginning of the stream, in units of 1/48000 seconds.
    /// </summary>
    public int PreSkip { get; init; }

    /// <summary>
    /// Gets the sample rate that the encoder was targeting when creating the opus stream.
    /// Defaults to 48000 Hz.
    /// </summary>
    public int InputSampleRate { get; init; } = 48000;

    /// <summary>
    /// Gets the amount of gain to apply when decoding, in units of 1/256 dB.
    /// </summary>
    public int OutputGain { get; init; }

    /// <summary>
    /// Gets the channel mapping family (0 = RTP Vorbis, 1 = Vorbis, 255 = Discrete).
    /// </summary>
    public int MappingFamily { get; init; }

    /// <summary>
    /// Gets the number of streams in the channel mapping.
    /// </summary>
    public required int StreamCount { get; init; }

    /// <summary>
    /// Gets the number of coupled streams in the channel mapping.
    /// </summary>
    public required int CoupledStreamCount { get; init; }

    /// <summary>
    /// Gets the channel mapping array specifying how input channels map to encoded streams.
    /// </summary>
    public required byte[] ChannelMapping { get; init; }

    /// <summary>
    /// Creates an OpusHead header for the specified channel configuration.
    /// </summary>
    /// <param name="channels">The number of audio channels.</param>
    /// <param name="preSkip">The number of samples to skip at the beginning, in units of 1/48000 seconds.</param>
    /// <param name="mappingFamily">The channel mapping family (0, 1, or 255).</param>
    /// <returns>A new OpusHeader configured for the specified channels and mapping family.</returns>
    /// <exception cref="AudioException">
    /// Thrown when the mapping family or channel count combination is not supported.
    /// </exception>
    public static OpusHeader Create(int channels, int preSkip, int mappingFamily)
    {
        (int streams, int coupledStreams, byte[] mapping) = mappingFamily switch
        {
            0 when channels is 1 or 2 => VorbisLayouts[channels - 1],
            1 when channels is >= 1 and <= 8 => VorbisLayouts[channels - 1],
            255 => (channels, 0, Enumerable.Range(0, channels).Select(channel => (byte)channel).ToArray()),
            _ => throw new AudioException($"Opus mapping family {mappingFamily} with {channels} channels is not supported."),
        };

        return new OpusHeader
        {
            Channels = channels,
            PreSkip = preSkip,
            MappingFamily = mappingFamily,
            StreamCount = streams,
            CoupledStreamCount = coupledStreams,
            ChannelMapping = mapping,
        };
    }

    /// <summary>
    /// Parses an OpusHead header from the supplied binary data.
    /// </summary>
    /// <param name="data">The binary data containing the OpusHead header.</param>
    /// <returns>A parsed OpusHeader instance.</returns>
    /// <exception cref="AudioException">
    /// Thrown when the data is not a valid OpusHead header or is truncated.
    /// </exception>
    public static OpusHeader Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < FixedSize || !data[..8].SequenceEqual(Magic))
            throw new AudioException("The Opus setup data is not an OpusHead header.");

        int channels = data[9];
        int mappingFamily = data[18];

        if (mappingFamily != 0 && data.Length < FixedSize + 2 + channels)
            throw new AudioException("The OpusHead header is truncated.");

        return new OpusHeader
        {
            Channels = channels,
            PreSkip = BinaryPrimitives.ReadUInt16LittleEndian(data[10..]),
            InputSampleRate = BinaryPrimitives.ReadInt32LittleEndian(data[12..]),
            OutputGain = BinaryPrimitives.ReadInt16LittleEndian(data[16..]),
            MappingFamily = mappingFamily,
            StreamCount = mappingFamily == 0 ? 1 : data[19],
            CoupledStreamCount = mappingFamily == 0 ? channels - 1 : data[20],
            ChannelMapping = mappingFamily == 0 ? VorbisLayouts[channels - 1].Mapping : data.Slice(21, channels).ToArray(),
        };
    }

    /// <summary>
    /// Serializes the header to binary OpusHead format.
    /// </summary>
    /// <returns>A byte array containing the serialized OpusHead header.</returns>
    public byte[] ToBytes()
    {
        byte[] data = new byte[FixedSize + (MappingFamily == 0 ? 0 : 2 + Channels)];

        Magic.CopyTo(data);
        data[8] = 1;
        data[9] = (byte)Channels;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(10), (ushort)PreSkip);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(12), InputSampleRate);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(16), (short)OutputGain);
        data[18] = (byte)MappingFamily;

        if (MappingFamily != 0)
        {
            data[19] = (byte)StreamCount;
            data[20] = (byte)CoupledStreamCount;
            ChannelMapping.CopyTo(data, 21);
        }

        return data;
    }
}
