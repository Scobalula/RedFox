// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Audio.Opus;

/// <summary>
/// Codec implementation for the Opus audio format.
/// Supports decoding Opus-encoded audio streams with packet-based playback.
/// </summary>
public sealed class OpusCodec : AudioCodec
{
    /// <inheritdoc/>
    public override string Id => "opus";

    /// <inheritdoc/>
    public override string Name => "Opus";

    /// <inheritdoc/>
    public override bool CanDecode => true;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when the supplied audio is null.</exception>
    public override AudioDecoder CreateDecoder(EncodedAudio audio)
    {
        ArgumentNullException.ThrowIfNull(audio);
        return new OpusDecoder(audio);
    }
}
