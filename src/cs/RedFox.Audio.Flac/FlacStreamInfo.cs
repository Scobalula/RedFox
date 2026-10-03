// --------------------------------------------------------------------------------------
// RedFox Utility Library - MIT License
// --------------------------------------------------------------------------------------
// Copyright (c) 2025 Philip/Scobalula
// --------------------------------------------------------------------------------------
// Please see LICENSE.md for license information.
// This library is also bound by 3rd party licenses.
// --------------------------------------------------------------------------------------

using System.Buffers.Binary;

namespace RedFox.Audio.Flac;

internal readonly record struct FlacStreamInfo(int MinBlockSize, int MaxBlockSize, int MinFrameSize, int MaxFrameSize, int SampleRate, int Channels, int BitsPerSample, long TotalSamples, UInt128 Md5)
{
    public const int Size = 34;

    public static FlacStreamInfo Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < Size)
            throw new AudioException("The FLAC STREAMINFO block is truncated.");

        ulong packed = BinaryPrimitives.ReadUInt64BigEndian(data[10..]);

        return new FlacStreamInfo(BinaryPrimitives.ReadUInt16BigEndian(data), BinaryPrimitives.ReadUInt16BigEndian(data[2..]), ReadUInt24(data[4..]), ReadUInt24(data[7..]), (int)(packed >> 44), (int)((packed >> 41) & 0x7) + 1, (int)((packed >> 36) & 0x1F) + 1, (long)(packed & 0xF_FFFF_FFFF), BinaryPrimitives.ReadUInt128BigEndian(data[18..]));
    }

    public void Write(Span<byte> destination)
    {
        ulong packed = ((ulong)SampleRate << 44) | ((ulong)(Channels - 1) << 41) | ((ulong)(BitsPerSample - 1) << 36) | ((ulong)TotalSamples & 0xF_FFFF_FFFF);

        BinaryPrimitives.WriteUInt16BigEndian(destination, (ushort)MinBlockSize);
        BinaryPrimitives.WriteUInt16BigEndian(destination[2..], (ushort)MaxBlockSize);
        WriteUInt24(destination[4..], MinFrameSize);
        WriteUInt24(destination[7..], MaxFrameSize);
        BinaryPrimitives.WriteUInt64BigEndian(destination[10..], packed);
        BinaryPrimitives.WriteUInt128BigEndian(destination[18..], Md5);
    }

    private static int ReadUInt24(ReadOnlySpan<byte> data) => (data[0] << 16) | (data[1] << 8) | data[2];

    private static void WriteUInt24(Span<byte> destination, int value)
    {
        destination[0] = (byte)(value >> 16);
        destination[1] = (byte)(value >> 8);
        destination[2] = (byte)value;
    }
}
