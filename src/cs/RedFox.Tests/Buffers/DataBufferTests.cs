using System.Numerics;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Buffers;
using RedFox.Graphics3D.Rendering;

namespace RedFox.Tests.Buffers;

public class DataBufferTests
{
    [Fact]
    public void DataBuffer_Add_ReservesElementAndReturnsCursor()
    {
        DataBuffer<float> buffer = new(2, 2, 3);

        var element = buffer.Add();

        Assert.Equal(1, buffer.ElementCount);
        Assert.Equal(0, element.ElementIndex);
        Assert.Equal(0, element.NextValueIndex);
        Assert.Equal(0, element.NextComponentIndex);
        Assert.False(element.IsComplete);
        Assert.Equal(0f, buffer.Get<float>(0, 1, 2));
    }

    [Fact]
    public void DataBuffer_AddScalar_AppendsSingleValueElement()
    {
        DataBuffer<float> buffer = new();

        var element = buffer.Add(5f);

        Assert.Equal(1, buffer.ElementCount);
        Assert.Equal(5f, buffer.Get<float>(0, 0, 0));
        Assert.Equal(1, element.NextValueIndex);
        Assert.Equal(0, element.NextComponentIndex);
        Assert.True(element.IsComplete);
    }

    [Fact]
    public void DataBufferElement_AddScalar_FillsMultiValueElementSequentially()
    {
        DataBuffer<int> buffer = new(0, 2, 2);

        var element = buffer.Add();
        element.Add(1);
        element.Add(2);
        element.Add(3);
        element.Add(4);

        Assert.Equal(1, buffer.Get<int>(0, 0, 0));
        Assert.Equal(2, buffer.Get<int>(0, 0, 1));
        Assert.Equal(3, buffer.Get<int>(0, 1, 0));
        Assert.Equal(4, buffer.Get<int>(0, 1, 1));
        Assert.Equal(2, element.NextValueIndex);
        Assert.Equal(0, element.NextComponentIndex);
        Assert.True(element.IsComplete);
    }

    [Fact]
    public void DataBuffer_AddVector3_AppendsElement()
    {
        DataBuffer<float> buffer = new(0, 1, 3);

        var element = buffer.Add(new Vector3(1f, 2f, 3f));

        Assert.Equal(1, buffer.ElementCount);
        Assert.Equal(1f, buffer.Get<float>(0, 0, 0));
        Assert.Equal(2f, buffer.Get<float>(0, 0, 1));
        Assert.Equal(3f, buffer.Get<float>(0, 0, 2));
        Assert.True(element.IsComplete);
    }

    [Fact]
    public void DataBufferElement_AddVector2_ThrowsWhenCrossingValueBoundary()
    {
        DataBuffer<float> buffer = new(0, 2, 2);

        var element = buffer.Add();
        element.Add(1f);

        Assert.Throws<InvalidOperationException>(() => element.Add(new Vector2(2f, 3f)));
        Assert.Equal(1f, buffer.Get<float>(0, 0, 0));
        Assert.Equal(0f, buffer.Get<float>(0, 0, 1));
    }

    [Fact]
    public void DataBuffer_CreateStrided_PacksContiguousValues()
    {
        byte[] source = new byte[sizeof(float) * 8];
        Span<float> sourceValues = MemoryMarshal.Cast<byte, float>(source.AsSpan());
        sourceValues[0] = 1f;
        sourceValues[1] = 2f;
        sourceValues[2] = 3f;
        sourceValues[3] = 99f;
        sourceValues[4] = 4f;
        sourceValues[5] = 5f;
        sourceValues[6] = 6f;
        sourceValues[7] = 100f;

        DataBuffer<float> buffer = DataBufferPacking.CreateStrided<float>(source, 2, byteOffset: 0, byteStride: sizeof(float) * 4, valueCount: 1, componentCount: 3, byteValueStride: sizeof(float) * 3);

        Assert.Equal(2, buffer.ElementCount);
        Assert.Equal(1, buffer.ValueCount);
        Assert.Equal(3, buffer.ComponentCount);
        Assert.Equal(new float[] { 1f, 2f, 3f, 4f, 5f, 6f }, buffer.ToArray<float>());
    }

    [Fact]
    public void DataBuffer_AsReadOnlySpan_MatchingType_IsLiveView()
    {
        DataBuffer<float> buffer = new(0, 1, 2);
        buffer.Add(0, 0, 0, 1f);
        buffer.Add(0, 0, 1, 2f);

        ReadOnlySpan<float> span = buffer.AsReadOnlySpan<float>();

        Assert.Equal(2, span.Length);
        Assert.Equal(1f, span[0]);
        Assert.Equal(2f, span[1]);
    }

    [Fact]
    public void DataBuffer_AsSpan_MatchingType_WritesThrough()
    {
        DataBuffer<int> buffer = new(0, 1, 2);
        buffer.Add(0, 0, 0, 3);
        buffer.Add(0, 0, 1, 4);

        Span<int> span = buffer.AsSpan<int>();
        span[1] = 99;

        Assert.Equal(99, buffer.Get<int>(0, 0, 1));
    }

    [Fact]
    public void DataBuffer_AsReadOnlySpan_DifferentType_FallsBackToConvertedCopy()
    {
        DataBuffer<int> buffer = new(0, 1, 2);
        buffer.Add(0, 0, 0, 10);
        buffer.Add(0, 0, 1, 20);

        ReadOnlySpan<float> span = buffer.AsReadOnlySpan<float>();

        Assert.Equal(2, span.Length);
        Assert.Equal(10f, span[0]);
        Assert.Equal(20f, span[1]);
    }

    [Fact]
    public void DataBuffer_GetValueArray_ReturnsValueComponents()
    {
        DataBuffer<float> buffer = new(0, 2, 3);
        buffer.Add(0, 0, 0, 1f);
        buffer.Add(0, 0, 1, 2f);
        buffer.Add(0, 0, 2, 3f);
        buffer.Add(0, 1, 0, 4f);
        buffer.Add(0, 1, 1, 5f);
        buffer.Add(0, 1, 2, 6f);

        float[] value = buffer.GetValueArray<float>(0, 1);

        Assert.Equal([4f, 5f, 6f], value);
    }

    [Fact]
    public void DataBuffer_GetElementArray_ReturnsElementValueMajorData()
    {
        DataBuffer<int> buffer = new(0, 2, 2);
        buffer.Add(0, 0, 0, 10);
        buffer.Add(0, 0, 1, 11);
        buffer.Add(0, 1, 0, 12);
        buffer.Add(0, 1, 1, 13);

        int[] element = buffer.GetElementArray<int>(0);

        Assert.Equal([10, 11, 12, 13], element);
    }

    [Fact]
    public void DataBuffer_ToArray_ReturnsAllElementsInOrder()
    {
        DataBuffer<int> buffer = new(0, 1, 2);
        buffer.Add(0, 0, 0, 1);
        buffer.Add(0, 0, 1, 2);
        buffer.Add(1, 0, 0, 3);
        buffer.Add(1, 0, 1, 4);

        int[] all = buffer.ToArray<int>();

        Assert.Equal([1, 2, 3, 4], all);
    }

    [Fact]
    public void DataBuffer_AsReadOnlySpanForElement_ReturnsValueMajorSlice()
    {
        DataBuffer<int> buffer = new(0, 2, 2);
        buffer.Add(0, 0, 0, 7);
        buffer.Add(0, 0, 1, 8);
        buffer.Add(0, 1, 0, 9);
        buffer.Add(0, 1, 1, 10);

        ReadOnlySpan<int> element = buffer.AsReadOnlySpan<int>(0);

        Assert.Equal(4, element.Length);
        Assert.Equal(7, element[0]);
        Assert.Equal(8, element[1]);
        Assert.Equal(9, element[2]);
        Assert.Equal(10, element[3]);
    }

    [Fact]
    public void DataBuffer_TryGetGpuBufferData_ReturnsTypedLiveByteView()
    {
        DataBuffer<ushort> buffer = new(new ushort[] { 1, 2, 3, 4, 5, 6 }, 1, 3);

        bool result = buffer.TryGetGpuBufferData(out GpuBufferData bufferData);
        ReadOnlySpan<ushort> values = MemoryMarshal.Cast<byte, ushort>(bufferData.Bytes);

        Assert.True(result);
        Assert.Equal(GpuBufferElementType.UInt16, bufferData.ElementType);
        Assert.Equal(2, bufferData.ElementCount);
        Assert.Equal(1, bufferData.ValueCount);
        Assert.Equal(3, bufferData.ComponentCount);
        Assert.Equal(sizeof(ushort) * 3, bufferData.ElementStrideBytes);
        Assert.Equal(sizeof(ushort) * 3, bufferData.ValueStrideBytes);
        Assert.Equal(sizeof(ushort), bufferData.ComponentSizeBytes);
        Assert.Equal(6, bufferData.TotalComponentCount);
        Assert.True(bufferData.IsTightlyPacked);
        Assert.Equal(new ushort[] { 1, 2, 3, 4, 5, 6 }, values.ToArray());

        buffer.AsSpan()[1] = 42;
        Assert.Equal(42, values[1]);
    }

    [Fact]
    public void DataBuffer_CreateStrided_TryGetGpuBufferData_ReturnsPackedByteView()
    {
        byte[] source = new byte[sizeof(float) * 8];
        Span<float> sourceValues = MemoryMarshal.Cast<byte, float>(source.AsSpan());
        sourceValues[0] = 1f;
        sourceValues[1] = 2f;
        sourceValues[2] = 3f;
        sourceValues[3] = 99f;
        sourceValues[4] = 4f;
        sourceValues[5] = 5f;
        sourceValues[6] = 6f;
        sourceValues[7] = 100f;
        DataBuffer<float> buffer = DataBufferPacking.CreateStrided<float>(source, 2, byteOffset: 0, byteStride: sizeof(float) * 4, valueCount: 1, componentCount: 3, byteValueStride: sizeof(float) * 3);

        bool result = buffer.TryGetGpuBufferData(out GpuBufferData bufferData);

        Assert.True(result);
        Assert.Equal(GpuBufferElementType.Float32, bufferData.ElementType);
        Assert.Equal(2, bufferData.ElementCount);
        Assert.Equal(sizeof(float) * 3, bufferData.ElementStrideBytes);
        Assert.True(bufferData.IsTightlyPacked);
        Assert.Equal(new float[] { 1f, 2f, 3f, 4f, 5f, 6f }, MemoryMarshal.Cast<byte, float>(bufferData.Bytes).ToArray());
    }
}
