// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Audio.IO.Wav;

internal sealed class WavPassthroughCodec(ushort formatTag) : AudioCodec
{
    public ushort FormatTag { get; } = formatTag;

    public override string Id => $"wav-format-{FormatTag:x4}";

    public override string Name => $"WAVE format 0x{FormatTag:X4}";
}
