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
/// Decoder that reads from a pre-decoded audio buffer.
/// </summary>
internal sealed class AudioBufferDecoder(AudioBuffer buffer) : AudioDecoder
{
    private int _position;

    /// <inheritdoc/>
    public override AudioFormat Format => buffer.Format;

    /// <inheritdoc/>
    public override SampleFormat SampleFormat => buffer.SampleFormat;

    /// <inheritdoc/>
    public override long FrameCount => buffer.FrameCount;

    /// <inheritdoc/>
    public override long Position => _position;

    /// <inheritdoc/>
    public override int ValidBitsPerSample => buffer.ValidBitsPerSample;

    /// <inheritdoc/>
    public override bool CanSeek => true;

    /// <inheritdoc/>
    public override int Read(Span<byte> destination)
    {
        int frames = Math.Min(destination.Length / BytesPerFrame, buffer.FrameCount - _position);

        buffer.GetFrames(_position, frames).CopyTo(destination);
        _position += frames;

        return frames;
    }

    public override void Seek(long frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(frame, buffer.FrameCount);

        _position = (int)frame;
    }
}
