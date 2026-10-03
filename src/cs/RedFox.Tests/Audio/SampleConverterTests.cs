using System.Runtime.InteropServices;
using RedFox.Audio;

namespace RedFox.Tests.Audio;

public sealed class SampleConverterTests
{
    [Theory]
    [InlineData(SampleFormat.Int24)]
    [InlineData(SampleFormat.Int32)]
    [InlineData(SampleFormat.Float32)]
    [InlineData(SampleFormat.Float64)]
    public void Convert_Int16ThroughWiderFormat_RoundTripsExactly(SampleFormat intermediate)
    {
        short[] source = [short.MinValue, -12345, -1, 0, 1, 12345, short.MaxValue];
        byte[] wide = new byte[source.Length * SampleFormatInfo.GetBytesPerSample(intermediate)];
        short[] result = new short[source.Length];

        SampleConverter.Convert(MemoryMarshal.AsBytes(source.AsSpan()), SampleFormat.Int16, wide, intermediate);
        SampleConverter.Convert(wide, intermediate, MemoryMarshal.AsBytes(result.AsSpan()), SampleFormat.Int16);

        Assert.Equal(source, result);
    }

    [Fact]
    public void Convert_FloatOutOfRange_ClampsToIntegerLimits()
    {
        float[] source = [-2.0f, -1.0f, 0.0f, 1.0f, 2.0f];
        short[] result = new short[source.Length];

        SampleConverter.Convert(MemoryMarshal.AsBytes(source.AsSpan()), SampleFormat.Float32, MemoryMarshal.AsBytes(result.AsSpan()), SampleFormat.Int16);

        Assert.Equal([short.MinValue, short.MinValue, 0, short.MaxValue, short.MaxValue], result);
    }

    [Fact]
    public void Convert_Int24_SignExtendsNegativeSamples()
    {
        byte[] source = [0x00, 0x00, 0x80, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F];
        float[] result = new float[3];

        SampleConverter.Convert(source, SampleFormat.Int24, MemoryMarshal.AsBytes(result.AsSpan()), SampleFormat.Float32);

        Assert.Equal(-1.0f, result[0]);
        Assert.Equal(-1.0f / 8388608.0f, result[1]);
        Assert.Equal(8388607.0f / 8388608.0f, result[2]);
    }

    [Fact]
    public void Convert_UInt8_UsesUnsignedMidpointAsSilence()
    {
        byte[] source = [0, 128, 255];
        short[] result = new short[3];

        SampleConverter.Convert(source, SampleFormat.UInt8, MemoryMarshal.AsBytes(result.AsSpan()), SampleFormat.Int16);

        Assert.Equal([short.MinValue, 0, 32512], result);
    }

    [Fact]
    public void Convert_Int16ToInt32_ShiftsIntoHighBits()
    {
        short[] source = [-2, 3];
        int[] result = new int[2];

        SampleConverter.Convert(MemoryMarshal.AsBytes(source.AsSpan()), SampleFormat.Int16, MemoryMarshal.AsBytes(result.AsSpan()), SampleFormat.Int32);

        Assert.Equal([-2 << 16, 3 << 16], result);
    }
}
