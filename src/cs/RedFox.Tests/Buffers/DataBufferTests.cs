using System.Numerics;
using RedFox.Graphics3D.Buffers;

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
    public void DataBufferView_Append_Throws()
    {
        var view = DataBufferView<float>.CreatePacked(new byte[sizeof(float) * 2]);

        Assert.Throws<NotSupportedException>(() => view.Add());
        Assert.Throws<NotSupportedException>(() => view.Add(1f));
    }
}
