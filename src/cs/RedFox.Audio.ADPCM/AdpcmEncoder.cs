// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Runtime.InteropServices;

namespace RedFox.Audio.ADPCM;

/// <summary>
/// Encoder for ADPCM audio formats.
/// </summary>
internal sealed class AdpcmEncoder : AudioEncoder
{
    private readonly AdpcmCodec _codec;
    private readonly int _blockAlign;
    private readonly int _framesPerBlock;
    private readonly short[] _pending;
    private readonly byte[] _block;
    private readonly int[] _channelState;
    private int _pendingFrames;

    /// <inheritdoc/>
    public override AudioFormat Format { get; }

    /// <inheritdoc/>
    public override SampleFormat InputSampleFormat => SampleFormat.Int16;

    /// <inheritdoc/>
    public override int BitsPerSample => 4;

    /// <inheritdoc/>
    public override int BlockAlign => _blockAlign;

    /// <inheritdoc/>
    public override ReadOnlyMemory<byte> Setup { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AdpcmEncoder"/> class.
    /// </summary>
    /// <param name="codec">The ADPCM codec to use.</param>
    /// <param name="format">The target audio format.</param>
    /// <param name="blockAlign">The block alignment in bytes.</param>
    public AdpcmEncoder(AdpcmCodec codec, AudioFormat format, int blockAlign)
    {
        _codec = codec;
        _blockAlign = blockAlign;
        _framesPerBlock = codec.GetFramesPerBlock(format.Channels, blockAlign);
        _pending = new short[_framesPerBlock * format.Channels];
        _block = new byte[blockAlign];
        _channelState = new int[format.Channels];

        Format = format;
        Setup = codec.CreateSetup(_framesPerBlock);
    }

    public override void Encode(ReadOnlySpan<byte> frames, IAudioPacketWriter output)
    {
        ReadOnlySpan<short> samples = MemoryMarshal.Cast<byte, short>(frames);
        int channels = Format.Channels;

        while (!samples.IsEmpty)
        {
            int count = Math.Min(samples.Length, _pending.Length - (_pendingFrames * channels));

            samples[..count].CopyTo(_pending.AsSpan(_pendingFrames * channels));
            _pendingFrames += count / channels;
            samples = samples[count..];

            if (_pendingFrames == _framesPerBlock)
                Flush(output);
        }
    }

    public override void Complete(IAudioPacketWriter output)
    {
        if (_pendingFrames == 0)
            return;

        _pending.AsSpan(_pendingFrames * Format.Channels).Clear();
        Flush(output);
    }

    private void Flush(IAudioPacketWriter output)
    {
        _codec.EncodeBlock(_pending, _block, Format.Channels, _channelState);
        output.WritePacket(_block, _pendingFrames);
        _pendingFrames = 0;
    }
}
