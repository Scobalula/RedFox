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
/// Base class for audio encoder implementations that convert audio samples into encoded format.
/// </summary>
public abstract class AudioEncoder : IDisposable
{
    /// <summary>
    /// Gets the target audio format for encoding.
    /// </summary>
    public abstract AudioFormat Format { get; }

    /// <summary>
    /// Gets the sample format expected by the encoder.
    /// </summary>
    public abstract SampleFormat InputSampleFormat { get; }

    /// <summary>
    /// Gets the valid bits per sample in the encoded output, or 0 if not applicable.
    /// </summary>
    public virtual int BitsPerSample => 0;

    /// <summary>
    /// Gets the block alignment of the encoded data, or 0 if not applicable.
    /// </summary>
    public virtual int BlockAlign => 0;

    /// <summary>
    /// Gets codec-specific setup or header data, or an empty memory if not applicable.
    /// </summary>
    public virtual ReadOnlyMemory<byte> Setup => ReadOnlyMemory<byte>.Empty;

    /// <summary>
    /// Encodes the supplied audio frames and writes the encoded data to the output.
    /// </summary>
    /// <param name="frames">The audio frames to encode, in the input sample format.</param>
    /// <param name="output">The writer that receives the encoded output.</param>
    public abstract void Encode(ReadOnlySpan<byte> frames, IAudioPacketWriter output);

    /// <summary>
    /// Completes the encoding operation and flushes any remaining data to the output.
    /// </summary>
    /// <param name="output">The writer that receives the final encoded output.</param>
    public abstract void Complete(IAudioPacketWriter output);

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
