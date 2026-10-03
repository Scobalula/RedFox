// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Audio.Flac;

internal readonly record struct FlacMetadataBlock(byte Type, ReadOnlyMemory<byte> Data)
{
    public const byte StreamInfo = 0;

    public const byte SeekTable = 3;

    public const byte VorbisComment = 4;
}
