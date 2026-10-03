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
/// Represents an audio clip that can be stored as encoded data, decoded samples, or both.
/// </summary>
public sealed class AudioClip
{
    private const int SkipChunkFrames = 4096;

    private AudioBuffer? _buffer;

    /// <summary>
    /// Gets the encoded audio data, if available.
    /// </summary>
    public EncodedAudio? Encoded { get; private set; }

    /// <summary>
    /// Gets the format of the audio.
    /// </summary>
    public AudioFormat Format => Encoded?.Format ?? _buffer!.Format;

    /// <summary>
    /// Gets the total number of frames in the audio, or -1 if the length is unknown.
    /// </summary>
    public long FrameCount => Encoded is { FrameCount: >= 0 } encoded ? encoded.FrameCount : _buffer?.FrameCount ?? -1;

    /// <summary>
    /// Gets the total duration of the audio.
    /// </summary>
    public TimeSpan Duration => FrameCount < 0 ? TimeSpan.Zero : TimeSpan.FromSeconds((double)FrameCount / Format.SampleRate);

    /// <summary>
    /// Gets a value indicating whether the audio has been decoded to an <see cref="AudioBuffer"/>.
    /// </summary>
    public bool IsDecoded => _buffer is not null;

    /// <summary>
    /// Gets or sets the loop start position in frames, or <see langword="null"/> if looping is not enabled.
    /// </summary>
    public long? LoopStart { get; set; }

    /// <summary>
    /// Gets or sets the loop end position in frames, or <see langword="null"/> if looping is not enabled.
    /// </summary>
    public long? LoopEnd { get; set; }

    /// <summary>
    /// Gets the descriptive tags of the audio, keyed by case-insensitive Vorbis comment field names
    /// such as <c>TITLE</c>, <c>ARTIST</c>, <c>ALBUM</c>, <c>DATE</c>, <c>GENRE</c>, and <c>COMMENT</c>.
    /// </summary>
    public Dictionary<string, string> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioClip"/> class with decoded audio buffer.
    /// </summary>
    /// <param name="buffer">The decoded audio buffer.</param>
    /// <exception cref="ArgumentNullException">Thrown when the buffer is null.</exception>
    public AudioClip(AudioBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _buffer = buffer;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioClip"/> class with encoded audio data.
    /// </summary>
    /// <param name="encoded">The encoded audio data.</param>
    /// <exception cref="ArgumentNullException">Thrown when the encoded audio is null.</exception>
    public AudioClip(EncodedAudio encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        Encoded = encoded;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioClip"/> class with both encoded and decoded audio.
    /// </summary>
    /// <param name="encoded">The encoded audio data.</param>
    /// <param name="buffer">The decoded audio buffer.</param>
    /// <exception cref="ArgumentNullException">Thrown when either parameter is null.</exception>
    public AudioClip(EncodedAudio encoded, AudioBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        ArgumentNullException.ThrowIfNull(buffer);

        Encoded = encoded;
        _buffer = buffer;
    }

    /// <summary>
    /// Gets or decodes the audio buffer, decoding on-demand if necessary.
    /// </summary>
    /// <returns>The decoded audio buffer.</returns>
    public AudioBuffer GetBuffer()
    {
        if (_buffer is null)
        {
            using AudioDecoder decoder = OpenDecoder();
            _buffer = decoder.ReadToEnd();
        }

        return _buffer;
    }

    /// <summary>
    /// Opens a decoder for streaming playback, starting from the beginning.
    /// </summary>
    /// <returns>A decoder instance ready to read audio data.</returns>
    public AudioDecoder OpenDecoder() => _buffer is not null ? new AudioBufferDecoder(_buffer) : Encoded!.Codec.CreateDecoder(Encoded);

    /// <summary>
    /// Opens a decoder for streaming playback, starting from the specified frame position.
    /// </summary>
    /// <param name="frame">The frame position to start decoding from.</param>
    /// <returns>A decoder instance positioned at the specified frame.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the frame is negative.</exception>
    public AudioDecoder OpenDecoder(long frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);

        AudioDecoder decoder = OpenDecoder();

        try
        {
            if (decoder.CanSeek)
                decoder.Seek(Math.Min(frame, decoder.FrameCount < 0 ? frame : decoder.FrameCount));
            else
                Skip(decoder, frame);
        }
        catch
        {
            decoder.Dispose();
            throw;
        }

        return decoder;
    }

    /// <summary>
    /// Replaces the decoded audio buffer and clears the encoded data reference.
    /// </summary>
    /// <param name="buffer">The new decoded audio buffer.</param>
    /// <exception cref="ArgumentNullException">Thrown when the buffer is null.</exception>
    public void SetBuffer(AudioBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        _buffer = buffer;
        Encoded = null;
    }

    private static void Skip(AudioDecoder decoder, long frames)
    {
        byte[] scratch = new byte[SkipChunkFrames * decoder.BytesPerFrame];

        while (frames > 0)
        {
            int read = decoder.Read(scratch.AsSpan(0, (int)Math.Min(frames, SkipChunkFrames) * decoder.BytesPerFrame));

            if (read == 0)
                break;

            frames -= read;
        }
    }
}
