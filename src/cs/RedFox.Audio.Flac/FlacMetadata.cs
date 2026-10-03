// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

namespace RedFox.Audio.Flac;

internal sealed class FlacMetadata(List<FlacMetadataBlock> blocks)
{
    public const int StreamInfoOffset = 8;

    public static ReadOnlySpan<byte> Magic => "fLaC"u8;

    public List<FlacMetadataBlock> Blocks { get; } = blocks;

    public FlacStreamInfo StreamInfo => FlacStreamInfo.Parse(Blocks[0].Data.Span);

    public int Size => Magic.Length + Blocks.Sum(block => 4 + block.Data.Length);

    public static bool IsFlac(ReadOnlySpan<byte> header) => header.StartsWith(Magic);

    public static FlacMetadata Parse(ReadOnlyMemory<byte> data)
    {
        ReadOnlySpan<byte> span = data.Span;

        if (!IsFlac(span))
            throw new AudioException("The data is not a FLAC stream.");

        List<FlacMetadataBlock> blocks = [];
        int offset = Magic.Length;
        bool last = false;

        while (!last)
        {
            if (offset + 4 > span.Length)
                throw new AudioException("The FLAC metadata is truncated.");

            int length = (span[offset + 1] << 16) | (span[offset + 2] << 8) | span[offset + 3];

            if (offset + 4 + length > span.Length)
                throw new AudioException("The FLAC metadata is truncated.");

            last = (span[offset] & 0x80) != 0;
            blocks.Add(new FlacMetadataBlock((byte)(span[offset] & 0x7F), data.Slice(offset + 4, length)));
            offset += 4 + length;
        }

        if (blocks[0].Type != FlacMetadataBlock.StreamInfo || blocks[0].Data.Length != FlacStreamInfo.Size)
            throw new AudioException("The FLAC stream does not start with a STREAMINFO block.");

        return new FlacMetadata(blocks);
    }

    public int IndexOf(byte type) => Blocks.FindIndex(block => block.Type == type);

    public void Write(Stream stream)
    {
        Span<byte> header = stackalloc byte[4];

        stream.Write(Magic);

        for (int i = 0; i < Blocks.Count; i++)
        {
            FlacMetadataBlock block = Blocks[i];

            header[0] = (byte)(block.Type | (i == Blocks.Count - 1 ? 0x80 : 0));
            header[1] = (byte)(block.Data.Length >> 16);
            header[2] = (byte)(block.Data.Length >> 8);
            header[3] = (byte)block.Data.Length;

            stream.Write(header);
            stream.Write(block.Data.Span);
        }
    }
}
