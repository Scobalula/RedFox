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
/// Base class for audio codec implementations that can decode and/or encode audio data.
/// </summary>
public abstract class AudioCodec
{
    /// <summary>
    /// Gets the unique identifier for this codec, used to distinguish it from others.
    /// </summary>
    public abstract string Id { get; }

    /// <summary>
    /// Gets the human-readable name of this codec.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets a value indicating whether this codec supports decoding.
    /// </summary>
    public virtual bool CanDecode => false;

    /// <summary>
    /// Gets a value indicating whether this codec supports encoding.
    /// </summary>
    public virtual bool CanEncode => false;

    /// <summary>
    /// Creates a decoder for the supplied encoded audio data.
    /// </summary>
    /// <param name="audio">The encoded audio to decode.</param>
    /// <returns>A decoder instance for the supplied audio.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the codec does not support decoding.
    /// </exception>
    public virtual AudioDecoder CreateDecoder(EncodedAudio audio) => throw new NotSupportedException($"The {Name} codec does not support decoding.");

    /// <summary>
    /// Creates an encoder for the supplied audio format using default options.
    /// </summary>
    /// <param name="format">The target audio format.</param>
    /// <returns>An encoder instance for the supplied format.</returns>
    public AudioEncoder CreateEncoder(AudioFormat format) => CreateEncoder(format, new AudioEncoderOptions());

    /// <summary>
    /// Creates an encoder for the supplied audio format using the specified options.
    /// </summary>
    /// <param name="format">The target audio format.</param>
    /// <param name="options">The encoder options to use.</param>
    /// <returns>An encoder instance for the supplied format and options.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the codec does not support encoding.
    /// </exception>
    public virtual AudioEncoder CreateEncoder(AudioFormat format, AudioEncoderOptions options) => throw new NotSupportedException($"The {Name} codec does not support encoding.");
}
