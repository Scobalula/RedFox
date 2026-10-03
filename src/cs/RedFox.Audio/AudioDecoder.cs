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
/// Base class for audio decoder implementations that convert encoded audio data into samples.
/// </summary>
public abstract class AudioDecoder : IDisposable
{
    private const int UnknownLengthChunkFrames = 65536;

    /// <summary>
    /// Gets the format of the decoded audio.
    /// </summary>
    public abstract AudioFormat Format { get; }

    /// <summary>
    /// Gets the sample format used by the decoded output.
    /// </summary>
    public abstract SampleFormat SampleFormat { get; }

    /// <summary>
    /// Gets the total number of frames in the audio, or a negative value if the length is unknown.
    /// </summary>
    public abstract long FrameCount { get; }

    /// <summary>
    /// Gets the current playback position in frames.
    /// </summary>
    public abstract long Position { get; }

    /// <summary>
    /// Gets the number of valid bits per sample in the decoded output.
    /// </summary>
    public virtual int ValidBitsPerSample => SampleFormatInfo.GetBitsPerSample(SampleFormat);

    /// <summary>
    /// Gets a value indicating whether the decoder supports seeking to arbitrary positions.
    /// </summary>
    public virtual bool CanSeek => false;

    /// <summary>
    /// Gets the number of bytes used by a single audio frame across all channels.
    /// </summary>
    public int BytesPerFrame => SampleFormatInfo.GetBytesPerSample(SampleFormat) * Format.Channels;

    /// <summary>
    /// Decodes the next frames of audio into the supplied destination buffer.
    /// </summary>
    /// <param name="destination">The buffer to fill with decoded samples.</param>
    /// <returns>The number of frames written to the destination.</returns>
    public abstract int Read(Span<byte> destination);

    /// <summary>
    /// Seeks to the specified frame position.
    /// </summary>
    /// <param name="frame">The frame position to seek to.</param>
    /// <exception cref="NotSupportedException">
    /// Thrown when the decoder does not support seeking.
    /// </exception>
    public virtual void Seek(long frame) => throw new NotSupportedException("This decoder does not support seeking.");

    /// <summary>
    /// Decodes all remaining frames from the current position to the end.
    /// </summary>
    /// <returns>
    /// An <see cref="AudioBuffer"/> containing all remaining decoded audio.
    /// </returns>
    public AudioBuffer ReadToEnd()
    {
        int bytesPerFrame = BytesPerFrame;
        bool knownLength = FrameCount >= 0;
        byte[] data = new byte[knownLength ? checked((int)((FrameCount - Position) * bytesPerFrame)) : UnknownLengthChunkFrames * bytesPerFrame];
        int written = 0;

        while (true)
        {
            if (written == data.Length)
            {
                if (knownLength)
                    break;

                Array.Resize(ref data, data.Length * 2);
            }

            int frames = Read(data.AsSpan(written));

            if (frames == 0)
                break;

            written += frames * bytesPerFrame;
        }

        return new AudioBuffer(Format, SampleFormat, data.AsMemory(0, written), ValidBitsPerSample);
    }

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
