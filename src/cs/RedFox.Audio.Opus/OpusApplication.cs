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
/// Specifies the intended application mode for the Opus encoder, which influences
/// the codec's internal algorithms and trade-offs between quality, latency, and bitrate.
/// </summary>
public enum OpusApplication
{
    /// <summary>
    /// Optimized for VoIP applications where low latency and speech intelligibility are prioritized.
    /// Uses the SILK codec internally.
    /// </summary>
    Voip = 2048,

    /// <summary>
    /// Optimized for general audio (e.g. music, mixed content) where fidelity is prioritized.
    /// Uses the CELT codec internally.
    /// </summary>
    Audio = 2049,

    /// <summary>
    /// Optimized for lowest possible latency, suitable for real-time applications like
    /// remote music performance where round-trip delay must be minimized.
    /// </summary>
    RestrictedLowDelay = 2051,
}
