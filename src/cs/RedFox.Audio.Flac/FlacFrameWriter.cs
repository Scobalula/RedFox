// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Audio.Flac;

internal sealed class FlacFrameWriter(Stream stream) : IAudioPacketWriter
{
    public void WritePacket(ReadOnlySpan<byte> packet, int frameCount) => stream.Write(packet);
}
