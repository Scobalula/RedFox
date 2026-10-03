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
/// Decoder for ADPCM-encoded audio.
/// </summary>
internal sealed class AdpcmDecoder : AudioDecoder
{
    private readonly AdpcmCodec _codec;
    private readonly EncodedAudio _encoded;
    private readonly int _framesPerBlock;
    private readonly short[] _block;
    private long _position;
    private int _blockIndex = -1;

    /// <inheritdoc/>
    public override AudioFormat Format => _encoded.Format;

    /// <inheritdoc/>
    public override SampleFormat SampleFormat => SampleFormat.Int16;

    /// <inheritdoc/>
    public override long FrameCount { get; }

    /// <inheritdoc/>
    public override long Position => _position;

    /// <inheritdoc/>
    public override bool CanSeek => true;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdpcmDecoder"/> class.
    /// </summary>
    /// <param name="codec">The ADPCM codec to use.</param>
    /// <param name="encoded">The encoded audio data.</param>
    public AdpcmDecoder(AdpcmCodec codec, EncodedAudio encoded)
    {
        _codec = codec;
        _encoded = encoded;
        _framesPerBlock = codec.GetFramesPerBlock(encoded.Format.Channels, encoded.BlockAlign);
        _block = new short[_framesPerBlock * encoded.Format.Channels];

        long blockFrames = (long)(encoded.Data.Length / encoded.BlockAlign) * _framesPerBlock;
        FrameCount = encoded.FrameCount >= 0 ? Math.Min(encoded.FrameCount, blockFrames) : blockFrames;
    }

    public override int Read(Span<byte> destination)
    {
        Span<short> output = MemoryMarshal.Cast<byte, short>(destination);
        int channels = Format.Channels;
        int frames = (int)Math.Min(output.Length / channels, FrameCount - _position);
        int written = 0;

        while (written < frames)
        {
            int blockIndex = (int)(_position / _framesPerBlock);
            int blockOffset = (int)(_position % _framesPerBlock);
            int count = Math.Min(frames - written, _framesPerBlock - blockOffset);

            if (blockIndex != _blockIndex)
            {
                _codec.DecodeBlock(_encoded.Data.Span.Slice(blockIndex * _encoded.BlockAlign, _encoded.BlockAlign), _block, channels);
                _blockIndex = blockIndex;
            }

            _block.AsSpan(blockOffset * channels, count * channels).CopyTo(output[(written * channels)..]);
            written += count;
            _position += count;
        }

        return frames;
    }

    public override void Seek(long frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(frame, FrameCount);

        _position = frame;
    }
}
