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
/// Receives encoded audio packets from an encoder.
/// </summary>
public interface IAudioPacketWriter
{
    /// <summary>
    /// Writes an encoded packet to the output.
    /// </summary>
    /// <param name="packet">The encoded packet data.</param>
    /// <param name="frameCount">The number of audio frames contained in the packet.</param>
    void WritePacket(ReadOnlySpan<byte> packet, int frameCount);
}
