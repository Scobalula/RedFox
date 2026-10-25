using System.Numerics;

namespace RedFox.Audio.Wwise;

internal static class VorbisHeaderBuilder
{
    private const int VorbisPacketIdentification = 1;

    private const int VorbisPacketComment = 3;

    private const int VorbisPacketSetup = 5;

    public static byte[] BuildIdentificationHeader(int channels, int sampleRate, int shortBlockExponent, int longBlockExponent)
    {
        var writer = new OggBitWriter(64);

        WritePacketStart(writer, VorbisPacketIdentification);

        writer.WriteBits(0, 32);
        writer.WriteBits((uint)channels, 8);
        writer.WriteBits((uint)sampleRate, 32);
        writer.WriteBits(0, 32);
        writer.WriteBits(0, 32);
        writer.WriteBits(0, 32);
        writer.WriteBits((uint)shortBlockExponent, 4);
        writer.WriteBits((uint)longBlockExponent, 4);
        writer.WriteBits(1, 1);

        return writer.ToArray();
    }

    public static byte[] BuildCommentHeader()
    {
        var writer = new OggBitWriter(64);

        WritePacketStart(writer, VorbisPacketComment);

        writer.WriteBits(0, 32);
        writer.WriteBits(0, 32);
        writer.WriteBits(1, 1);

        return writer.ToArray();
    }

    public static byte[] BuildSetupHeader(ReadOnlySpan<byte> wwiseSetup, PackedCodebooks codebooks, int channels, out bool[] modeBlockFlags)
    {
        var reader = new OggBitReader(wwiseSetup);
        var writer = new OggBitWriter(wwiseSetup.Length * 4);

        WritePacketStart(writer, VorbisPacketSetup);

        var codebookCount = (int)TransferField(ref reader, writer, 8) + 1;

        for (var i = 0; i < codebookCount; i++)
            ConvertPackedCodebook(codebooks.GetCodebook((int)reader.ReadBits(10)), writer);

        writer.WriteBits(0, 6);
        writer.WriteBits(0, 16);

        var floorCount = (int)TransferField(ref reader, writer, 6) + 1;

        for (var i = 0; i < floorCount; i++)
        {
            writer.WriteBits(1, 16);
            TransferFloor(ref reader, writer);
        }

        var residueCount = (int)TransferField(ref reader, writer, 6) + 1;

        for (var i = 0; i < residueCount; i++)
        {
            writer.WriteBits(reader.ReadBits(2), 16);
            TransferResidue(ref reader, writer);
        }

        var mappingCount = (int)TransferField(ref reader, writer, 6) + 1;

        for (var i = 0; i < mappingCount; i++)
        {
            writer.WriteBits(0, 16);
            TransferMapping(ref reader, writer, channels);
        }

        var modeCount = (int)TransferField(ref reader, writer, 6) + 1;

        modeBlockFlags = new bool[modeCount];

        for (var i = 0; i < modeCount; i++)
        {
            modeBlockFlags[i] = TransferField(ref reader, writer, 1) != 0;

            writer.WriteBits(0, 16);
            writer.WriteBits(0, 16);

            TransferField(ref reader, writer, 8);
        }

        writer.WriteBits(1, 1);

        return writer.ToArray();
    }

    private static void WritePacketStart(OggBitWriter writer, int packetType)
    {
        writer.WriteBits((uint)packetType, 8);

        foreach (var character in "vorbis"u8)
            writer.WriteBits(character, 8);
    }

    private static uint TransferField(ref OggBitReader reader, OggBitWriter writer, int bits)
    {
        var value = reader.ReadBits(bits);

        writer.WriteBits(value, bits);

        return value;
    }

    private static void TransferFloor(ref OggBitReader reader, OggBitWriter writer)
    {
        var partitionCount = (int)TransferField(ref reader, writer, 5);
        var partitionClasses = new int[partitionCount];
        var maxClass = -1;

        for (var i = 0; i < partitionCount; i++)
        {
            partitionClasses[i] = (int)TransferField(ref reader, writer, 4);
            maxClass = Math.Max(maxClass, partitionClasses[i]);
        }

        var classDimensions = new int[maxClass + 1];

        for (var i = 0; i <= maxClass; i++)
        {
            classDimensions[i] = (int)TransferField(ref reader, writer, 3) + 1;

            var subclasses = (int)TransferField(ref reader, writer, 2);

            if (subclasses != 0)
                TransferField(ref reader, writer, 8);

            for (var j = 0; j < 1 << subclasses; j++)
                TransferField(ref reader, writer, 8);
        }

        TransferField(ref reader, writer, 2);

        var rangeBits = (int)TransferField(ref reader, writer, 4);
        var valueCount = 2;

        for (var i = 0; i < partitionCount; i++)
            valueCount += classDimensions[partitionClasses[i]];

        for (var i = 2; i < valueCount; i++)
            TransferField(ref reader, writer, rangeBits);
    }

    private static void TransferResidue(ref OggBitReader reader, OggBitWriter writer)
    {
        TransferField(ref reader, writer, 24);
        TransferField(ref reader, writer, 24);
        TransferField(ref reader, writer, 24);

        var classifications = (int)TransferField(ref reader, writer, 6) + 1;

        TransferField(ref reader, writer, 8);

        var cascade = new int[classifications];

        for (var i = 0; i < classifications; i++)
        {
            var lowBits = (int)TransferField(ref reader, writer, 3);
            var highBits = 0;

            if (TransferField(ref reader, writer, 1) != 0)
                highBits = (int)TransferField(ref reader, writer, 5);

            cascade[i] = lowBits | (highBits << 3);
        }

        for (var i = 0; i < classifications; i++)
        {
            for (var bit = 0; bit < 8; bit++)
            {
                if ((cascade[i] & (1 << bit)) != 0)
                    TransferField(ref reader, writer, 8);
            }
        }
    }

    private static void TransferMapping(ref OggBitReader reader, OggBitWriter writer, int channels)
    {
        var submaps = 1;

        if (TransferField(ref reader, writer, 1) != 0)
            submaps = (int)TransferField(ref reader, writer, 4) + 1;

        if (TransferField(ref reader, writer, 1) != 0)
        {
            var couplingSteps = (int)TransferField(ref reader, writer, 8) + 1;
            var channelBits = BitCount((uint)(channels - 1));

            for (var i = 0; i < couplingSteps; i++)
            {
                TransferField(ref reader, writer, channelBits);
                TransferField(ref reader, writer, channelBits);
            }
        }

        TransferField(ref reader, writer, 2);

        if (submaps > 1)
        {
            for (var i = 0; i < channels; i++)
                TransferField(ref reader, writer, 4);
        }

        for (var i = 0; i < submaps; i++)
        {
            TransferField(ref reader, writer, 8);
            TransferField(ref reader, writer, 8);
            TransferField(ref reader, writer, 8);
        }
    }

    private static void ConvertPackedCodebook(ReadOnlySpan<byte> packedCodebook, OggBitWriter writer)
    {
        var reader = new OggBitReader(packedCodebook);
        var dimensions = (int)reader.ReadBits(4);
        var entries = (int)reader.ReadBits(14);

        writer.WriteBits(0x564342, 24);
        writer.WriteBits((uint)dimensions, 16);
        writer.WriteBits((uint)entries, 24);

        var ordered = reader.ReadBit();

        writer.WriteBits(ordered ? 1u : 0u, 1);

        if (ordered)
        {
            writer.WriteBits(reader.ReadBits(5), 5);

            for (var remaining = entries; remaining > 0;)
            {
                var bits = BitCount((uint)remaining);
                var count = reader.ReadBits(bits);

                writer.WriteBits(count, bits);

                remaining -= (int)count;
            }
        }
        else
        {
            var lengthBits = (int)reader.ReadBits(3);
            var sparse = reader.ReadBit();

            writer.WriteBits(sparse ? 1u : 0u, 1);

            for (var i = 0; i < entries; i++)
            {
                if (sparse)
                {
                    var used = reader.ReadBit();

                    writer.WriteBits(used ? 1u : 0u, 1);

                    if (!used)
                        continue;
                }

                writer.WriteBits(reader.ReadBits(lengthBits), 5);
            }
        }

        var lookupType = reader.ReadBits(1);

        writer.WriteBits(lookupType, 4);

        if (lookupType != 1)
            return;

        writer.WriteBits(reader.ReadBits(32), 32);
        writer.WriteBits(reader.ReadBits(32), 32);

        var valueBitsLessOne = reader.ReadBits(4);
        var valueBits = (int)valueBitsLessOne + 1;

        writer.WriteBits(valueBitsLessOne, 4);
        writer.WriteBits(reader.ReadBits(1), 1);

        for (var i = 0; i < CountLookupValues(entries, dimensions); i++)
            writer.WriteBits(reader.ReadBits(valueBits), valueBits);
    }

    private static int BitCount(uint value)
    {
        return 32 - BitOperations.LeadingZeroCount(value);
    }

    private static int CountLookupValues(int entries, int dimensions)
    {
        if (dimensions == 0)
            return 0;

        var root = (int)Math.Floor(Math.Exp(Math.Log(entries) / dimensions));

        while (Power(root + 1, dimensions) <= entries)
            root++;

        while (Power(root, dimensions) > entries)
            root--;

        return root;
    }

    private static long Power(long value, int exponent)
    {
        long result = 1;

        for (var i = 0; i < exponent; i++)
        {
            result *= value;

            if (result > int.MaxValue)
                return result;
        }

        return result;
    }
}
