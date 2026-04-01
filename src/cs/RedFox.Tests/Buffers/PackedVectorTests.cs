using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RedFox.Graphics3D.Buffers.PackedVector;

namespace RedFox.Tests.Buffers;

public class PackedVectorTests
{
    private const float Epsilon = 0.001f;

    private static bool ApproxEqual(Vector4 a, Vector4 b, float epsilon = Epsilon)
    {
        return MathF.Abs(a.X - b.X) < epsilon
            && MathF.Abs(a.Y - b.Y) < epsilon
            && MathF.Abs(a.Z - b.Z) < epsilon
            && MathF.Abs(a.W - b.W) < epsilon;
    }

    private static void AssertRoundtrip<TPacked>(Vector4 input, float epsilon = Epsilon) where TPacked : unmanaged, IPackedVector<TPacked>
    {
        var packed = default(TPacked);
        packed.Pack(input);
        var output = packed.Unpack();

        for (int i = 0; i < TPacked.ComponentCount; i++)
        {
            var expected = i switch { 0 => input.X, 1 => input.Y, 2 => input.Z, _ => input.W };
            var actual = i switch { 0 => output.X, 1 => output.Y, 2 => output.Z, _ => output.W };
            Assert.True(MathF.Abs(expected - actual) < epsilon,
                $"Type {typeof(TPacked).Name}: component {i} roundtrip failed. Expected {expected}, got {actual}");
        }
    }

    [Fact] public void Half2_Roundtrip_Zero() => AssertRoundtrip<Half2>(new Vector4(0f, 0f, 0f, 0f));
    [Fact] public void Half2_Roundtrip_Positive() => AssertRoundtrip<Half2>(new Vector4(0.5f, -0.25f, 0f, 0f));
    [Fact] public void Half2_Roundtrip_One() => AssertRoundtrip<Half2>(new Vector4(1f, 1f, 0f, 0f));
    [Fact] public void Half2_Roundtrip_Negative() => AssertRoundtrip<Half2>(new Vector4(-1f, -1f, 0f, 0f));
    [Fact] public void Half2_PackedValue()
    {
        var p = new Half2(1.0f, 0.0f);
        Assert.NotEqual(0u, p.PackedValue);
        var u = p.Unpack();
        Assert.True(MathF.Abs(u.X - 1.0f) < Epsilon);
        Assert.True(MathF.Abs(u.Y - 0.0f) < Epsilon);
    }

    [Fact] public void Half4_Roundtrip_Zero() => AssertRoundtrip<Half4>(Vector4.Zero);
    [Fact] public void Half4_Roundtrip_Positive() => AssertRoundtrip<Half4>(new Vector4(0.5f, -0.25f, 1.0f, -1.0f));
    [Fact] public void Half4_Roundtrip_One() => AssertRoundtrip<Half4>(Vector4.One);
    [Fact] public void Half4_ComponentCount() => Assert.Equal(4, Half4.ComponentCount);

    [Fact] public void ShortN2_Roundtrip_Zero() => AssertRoundtrip<ShortN2>(Vector4.Zero);
    [Fact] public void ShortN2_Roundtrip_One() => AssertRoundtrip<ShortN2>(new Vector4(1f, 1f, 0f, 0f));
    [Fact] public void ShortN2_Roundtrip_NegativeOne() => AssertRoundtrip<ShortN2>(new Vector4(-1f, -1f, 0f, 0f));
    [Fact] public void ShortN2_Roundtrip_Mid() => AssertRoundtrip<ShortN2>(new Vector4(0.5f, -0.5f, 0f, 0f));

    [Fact] public void Short2_Roundtrip_Zero() => AssertRoundtrip<Short2>(Vector4.Zero);
    [Fact] public void Short2_Roundtrip_Positive() => AssertRoundtrip<Short2>(new Vector4(100f, -200f, 0f, 0f));
    [Fact] public void Short2_ComponentCount() => Assert.Equal(2, Short2.ComponentCount);

    [Fact] public void UShortN2_Roundtrip_Zero() => AssertRoundtrip<UShortN2>(Vector4.Zero);
    [Fact] public void UShortN2_Roundtrip_One() => AssertRoundtrip<UShortN2>(new Vector4(1f, 1f, 0f, 0f));
    [Fact] public void UShortN2_Roundtrip_Mid() => AssertRoundtrip<UShortN2>(new Vector4(0.5f, 0.25f, 0f, 0f));

    [Fact] public void UShort2_Roundtrip_Zero() => AssertRoundtrip<UShort2>(Vector4.Zero);
    [Fact] public void UShort2_Roundtrip_Positive() => AssertRoundtrip<UShort2>(new Vector4(100f, 200f, 0f, 0f));

    [Fact] public void ByteN2_Roundtrip_Zero() => AssertRoundtrip<ByteN2>(Vector4.Zero);
    [Fact] public void ByteN2_Roundtrip_One() => AssertRoundtrip<ByteN2>(new Vector4(1f, 1f, 0f, 0f));
    [Fact] public void ByteN2_Roundtrip_NegativeOne() => AssertRoundtrip<ByteN2>(new Vector4(-1f, -1f, 0f, 0f));

    [Fact] public void Byte2_Roundtrip_Zero() => AssertRoundtrip<Byte2>(Vector4.Zero);
    [Fact] public void Byte2_Roundtrip_Positive() => AssertRoundtrip<Byte2>(new Vector4(50f, -50f, 0f, 0f));

    [Fact] public void UByteN2_Roundtrip_Zero() => AssertRoundtrip<UByteN2>(Vector4.Zero);
    [Fact] public void UByteN2_Roundtrip_One() => AssertRoundtrip<UByteN2>(new Vector4(1f, 1f, 0f, 0f));
    [Fact] public void UByteN2_Roundtrip_Mid() => AssertRoundtrip<UByteN2>(new Vector4(0.5f, 0.5f, 0f, 0f), 0.01f);

    [Fact] public void UByte2_Roundtrip_Zero() => AssertRoundtrip<UByte2>(Vector4.Zero);
    [Fact] public void UByte2_Roundtrip_Positive() => AssertRoundtrip<UByte2>(new Vector4(100f, 200f, 0f, 0f));

    [Fact] public void ShortN4_Roundtrip_Zero() => AssertRoundtrip<ShortN4>(Vector4.Zero);
    [Fact] public void ShortN4_Roundtrip_One() => AssertRoundtrip<ShortN4>(Vector4.One);
    [Fact] public void ShortN4_Roundtrip_NegativeOne() => AssertRoundtrip<ShortN4>(new Vector4(-1f, -1f, -1f, -1f));
    [Fact] public void ShortN4_Roundtrip_Mixed() => AssertRoundtrip<ShortN4>(new Vector4(0.5f, -0.5f, 0.25f, -0.75f));

    [Fact] public void Short4_Roundtrip_Zero() => AssertRoundtrip<Short4>(Vector4.Zero);
    [Fact] public void Short4_Roundtrip_Positive() => AssertRoundtrip<Short4>(new Vector4(100f, -200f, 300f, -400f));

    [Fact] public void UShortN4_Roundtrip_Zero() => AssertRoundtrip<UShortN4>(Vector4.Zero);
    [Fact] public void UShortN4_Roundtrip_One() => AssertRoundtrip<UShortN4>(Vector4.One);
    [Fact] public void UShortN4_Roundtrip_Mid() => AssertRoundtrip<UShortN4>(new Vector4(0.5f, 0.25f, 0.75f, 0.125f));

    [Fact] public void UShort4_Roundtrip_Zero() => AssertRoundtrip<UShort4>(Vector4.Zero);
    [Fact] public void UShort4_Roundtrip_Positive() => AssertRoundtrip<UShort4>(new Vector4(100f, 200f, 300f, 400f));

    [Fact] public void ByteN4_Roundtrip_Zero() => AssertRoundtrip<ByteN4>(Vector4.Zero);
    [Fact] public void ByteN4_Roundtrip_One() => AssertRoundtrip<ByteN4>(Vector4.One);
    [Fact] public void ByteN4_Roundtrip_NegativeOne() => AssertRoundtrip<ByteN4>(new Vector4(-1f, -1f, -1f, -1f));

    [Fact] public void Byte4_Roundtrip_Zero() => AssertRoundtrip<Byte4>(Vector4.Zero);
    [Fact] public void Byte4_Roundtrip_Positive() => AssertRoundtrip<Byte4>(new Vector4(50f, -50f, 100f, -100f));

    [Fact] public void UByteN4_Roundtrip_Zero() => AssertRoundtrip<UByteN4>(Vector4.Zero);
    [Fact] public void UByteN4_Roundtrip_One() => AssertRoundtrip<UByteN4>(Vector4.One);
    [Fact] public void UByteN4_Roundtrip_Mid() => AssertRoundtrip<UByteN4>(new Vector4(0.5f, 0.5f, 0.5f, 0.5f), 0.01f);

    [Fact] public void UByte4_Roundtrip_Zero() => AssertRoundtrip<UByte4>(Vector4.Zero);
    [Fact] public void UByte4_Roundtrip_Positive() => AssertRoundtrip<UByte4>(new Vector4(100f, 200f, 50f, 150f));

    [Fact] public void Color_Roundtrip_Zero() => AssertRoundtrip<Color>(Vector4.Zero);
    [Fact] public void Color_Roundtrip_One() => AssertRoundtrip<Color>(Vector4.One);
    [Fact] public void Color_Roundtrip_Mid() => AssertRoundtrip<Color>(new Vector4(0.5f, 0.25f, 0.75f, 1.0f), 0.01f);
    [Fact] public void Color_ComponentCount() => Assert.Equal(4, Color.ComponentCount);

    [Fact] public void U565_Roundtrip_Zero() => AssertRoundtrip<U565>(Vector4.Zero);
    [Fact] public void U565_Roundtrip_Max() => AssertRoundtrip<U565>(new Vector4(31f, 63f, 31f, 0f));
    [Fact] public void U565_Roundtrip_Mid() => AssertRoundtrip<U565>(new Vector4(15f, 32f, 10f, 0f));
    [Fact] public void U565_ComponentCount() => Assert.Equal(3, U565.ComponentCount);

    [Fact] public void U555_Roundtrip_Zero() => AssertRoundtrip<U555>(Vector4.Zero);
    [Fact] public void U555_Roundtrip_Max() => AssertRoundtrip<U555>(new Vector4(31f, 31f, 31f, 1f));
    [Fact] public void U555_Roundtrip_Mid() => AssertRoundtrip<U555>(new Vector4(15f, 10f, 20f, 0f));

    [Fact] public void UNibble4_Roundtrip_Zero() => AssertRoundtrip<UNibble4>(Vector4.Zero);
    [Fact] public void UNibble4_Roundtrip_Max() => AssertRoundtrip<UNibble4>(new Vector4(15f, 15f, 15f, 15f));
    [Fact] public void UNibble4_Roundtrip_Mid() => AssertRoundtrip<UNibble4>(new Vector4(5f, 10f, 3f, 7f));

    [Fact] public void XDecN4_Roundtrip_Zero() => AssertRoundtrip<XDecN4>(Vector4.Zero);
    [Fact] public void XDecN4_Roundtrip_One() => AssertRoundtrip<XDecN4>(new Vector4(1f, 1f, 1f, 1f));
    [Fact] public void XDecN4_Roundtrip_NegativeOne() => AssertRoundtrip<XDecN4>(new Vector4(-1f, -1f, -1f, 0f));
    [Fact] public void XDecN4_Roundtrip_Mixed() => AssertRoundtrip<XDecN4>(new Vector4(0.5f, -0.5f, 0.25f, 0.667f), 0.005f);

    [Fact] public void UDecN4_Roundtrip_Zero() => AssertRoundtrip<UDecN4>(Vector4.Zero);
    [Fact] public void UDecN4_Roundtrip_One() => AssertRoundtrip<UDecN4>(Vector4.One);
    [Fact] public void UDecN4_Roundtrip_Mid() => AssertRoundtrip<UDecN4>(new Vector4(0.5f, 0.25f, 0.75f, 0.333f), 0.005f);

    [Fact] public void UDec4_Roundtrip_Zero() => AssertRoundtrip<UDec4>(Vector4.Zero);
    [Fact] public void UDec4_Roundtrip_Max() => AssertRoundtrip<UDec4>(new Vector4(1023f, 1023f, 1023f, 3f));
    [Fact] public void UDec4_Roundtrip_Mid() => AssertRoundtrip<UDec4>(new Vector4(500f, 250f, 750f, 1f));

    [Fact] public void Float3PK_Roundtrip_Zero() => AssertRoundtrip<Float3PK>(Vector4.Zero);
    [Fact] public void Float3PK_Roundtrip_Positive() => AssertRoundtrip<Float3PK>(new Vector4(1.0f, 0.5f, 0.25f, 0f));
    [Fact] public void Float3PK_Roundtrip_Small() => AssertRoundtrip<Float3PK>(new Vector4(0.01f, 0.001f, 0.1f, 0f), 0.01f);
    [Fact] public void Float3PK_Negative_ClampsToZero()
    {
        var p = default(Float3PK);
        p.Pack(new Vector4(-1f, -1f, -1f, 0f));
        var u = p.Unpack();
        Assert.Equal(0f, u.X);
        Assert.Equal(0f, u.Y);
        Assert.Equal(0f, u.Z);
    }
    [Fact] public void Float3PK_ComponentCount() => Assert.Equal(3, Float3PK.ComponentCount);

    [Fact] public void Float3SE_Roundtrip_Zero() => AssertRoundtrip<Float3SE>(Vector4.Zero);
    [Fact] public void Float3SE_Roundtrip_Positive() => AssertRoundtrip<Float3SE>(new Vector4(1.0f, 0.5f, 0.25f, 0f), 0.05f);
    [Fact] public void Float3SE_Negative_ClampsToZero()
    {
        var p = default(Float3SE);
        p.Pack(new Vector4(-1f, -1f, -1f, 0f));
        var u = p.Unpack();
        Assert.Equal(0f, u.X);
        Assert.Equal(0f, u.Y);
        Assert.Equal(0f, u.Z);
    }
    [Fact] public void Float3SE_ComponentCount() => Assert.Equal(3, Float3SE.ComponentCount);

    [Fact]
    public void PackedBuffer_AddAndGet()
    {
        var buffer = new PackedBuffer<UByteN4>(10);
        Assert.Equal(0, buffer.ElementCount);
        Assert.Equal(4, buffer.ComponentCount);

        buffer.Add(0, 0, 0, 1.0f);
        buffer.Add(0, 0, 1, 0.5f);
        buffer.Add(0, 0, 2, 0.25f);
        buffer.Add(0, 0, 3, 0.75f);

        Assert.Equal(1, buffer.ElementCount);
        Assert.Equal(1.0f, buffer.Get<float>(0, 0, 0), Epsilon);
        Assert.Equal(0.5f, buffer.Get<float>(0, 0, 1), 0.01f);
        Assert.Equal(0.25f, buffer.Get<float>(0, 0, 2), Epsilon);
        Assert.Equal(0.75f, buffer.Get<float>(0, 0, 3), 0.01f);
    }

    [Fact]
    public void PackedBuffer_Set_OverwritesValue()
    {
        var buffer = new PackedBuffer<UByteN4>(10);
        buffer.Add(0, 0, 0, 1.0f);
        buffer.Add(0, 0, 1, 1.0f);
        buffer.Add(0, 0, 2, 1.0f);
        buffer.Add(0, 0, 3, 1.0f);

        buffer.Set(0, 0, 1, 0.0f);

        Assert.Equal(1.0f, buffer.Get<float>(0, 0, 0), Epsilon);
        Assert.Equal(0.0f, buffer.Get<float>(0, 0, 1), Epsilon);
        Assert.Equal(1.0f, buffer.Get<float>(0, 0, 2), Epsilon);
        Assert.Equal(1.0f, buffer.Get<float>(0, 0, 3), Epsilon);
    }

    [Fact]
    public void PackedBuffer_ScaleOffset()
    {
        var scale = new Vector4(2.0f, 2.0f, 2.0f, 2.0f);
        var offset = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
        var buffer = new PackedBuffer<UByteN4>(10, 1, scale, offset);

        buffer.Add(0, 0, 0, 3.0f);
        var got = buffer.Get<float>(0, 0, 0);
        Assert.Equal(3.0f, got, Epsilon);

        var packed = buffer.AsSpan()[0];
        var unpacked = packed.Unpack();
        Assert.Equal(1.0f, unpacked.X, Epsilon);
    }

    [Fact]
    public void PackedBuffer_MultipleElements()
    {
        var buffer = new PackedBuffer<Half2>(10);
        for (int i = 0; i < 5; i++)
        {
            buffer.Add(i, 0, 0, (float)i);
            buffer.Add(i, 0, 1, (float)(i * 10));
        }

        Assert.Equal(5, buffer.ElementCount);

        for (int i = 0; i < 5; i++)
        {
            Assert.Equal((float)i, buffer.Get<float>(i, 0, 0), Epsilon);
            Assert.Equal((float)(i * 10), buffer.Get<float>(i, 0, 1), Epsilon);
        }
    }

    [Fact]
    public void PackedBuffer_ValueCount_MultiDimension()
    {
        var buffer = new PackedBuffer<ShortN2>(10, 2);
        buffer.Add(0, 0, 0, 0.5f);
        buffer.Add(0, 0, 1, -0.5f);
        buffer.Add(0, 1, 0, 0.25f);
        buffer.Add(0, 1, 1, -0.25f);

        Assert.Equal(1, buffer.ElementCount);
        Assert.Equal(2, buffer.ValueCount);
        Assert.Equal(2, buffer.ComponentCount);

        Assert.Equal(0.5f, buffer.Get<float>(0, 0, 0), Epsilon);
        Assert.Equal(-0.5f, buffer.Get<float>(0, 0, 1), Epsilon);
        Assert.Equal(0.25f, buffer.Get<float>(0, 1, 0), Epsilon);
        Assert.Equal(-0.25f, buffer.Get<float>(0, 1, 1), Epsilon);
    }

    [Fact]
    public void PackedBuffer_FromByteArray()
    {
        var original = new PackedBuffer<UByte4>(4);
        original.Add(0, 0, 0, 10f);
        original.Add(0, 0, 1, 20f);
        original.Add(0, 0, 2, 30f);
        original.Add(0, 0, 3, 40f);

        var bytes = new byte[original.AsSpan().Length * Unsafe.SizeOf<UByte4>()];
        MemoryMarshal.Cast<UByte4, byte>(original.AsSpan()).CopyTo(bytes);

        var fromBytes = new PackedBuffer<UByte4>(bytes);
        Assert.Equal(1, fromBytes.ElementCount);
        Assert.Equal(10f, fromBytes.Get<float>(0, 0, 0));
        Assert.Equal(20f, fromBytes.Get<float>(0, 0, 1));
        Assert.Equal(30f, fromBytes.Get<float>(0, 0, 2));
        Assert.Equal(40f, fromBytes.Get<float>(0, 0, 3));
    }

    [Fact]
    public void PackedBuffer_FromPackedArray()
    {
        var packed = new UByteN4();
        packed.Pack(new Vector4(0.5f, 0.25f, 0.75f, 1.0f));

        var buffer = new PackedBuffer<UByteN4>([packed]);
        Assert.Equal(1, buffer.ElementCount);
        Assert.Equal(0.5f, buffer.Get<float>(0, 0, 0), 0.01f);
        Assert.Equal(0.25f, buffer.Get<float>(0, 0, 1), Epsilon);
        Assert.Equal(0.75f, buffer.Get<float>(0, 0, 2), 0.01f);
        Assert.Equal(1.0f, buffer.Get<float>(0, 0, 3), Epsilon);
    }

    [Fact]
    public void PackedBuffer_GetVector4()
    {
        var buffer = new PackedBuffer<Color>(4);
        buffer.Add(0, 0, 0, 0.5f);
        buffer.Add(0, 0, 1, 0.25f);
        buffer.Add(0, 0, 2, 0.75f);
        buffer.Add(0, 0, 3, 1.0f);

        var v = buffer.GetVector4(0, 0);
        Assert.True(MathF.Abs(v.X - 0.5f) < 0.01f);
        Assert.True(MathF.Abs(v.Y - 0.25f) < 0.01f);
        Assert.True(MathF.Abs(v.Z - 0.75f) < 0.01f);
        Assert.True(MathF.Abs(v.W - 1.0f) < 0.01f);
    }

    [Fact]
    public void PackedBuffer_ToArray_CopyTo()
    {
        var buffer = new PackedBuffer<Half4>(4);
        buffer.Add(0, 0, 0, 1f);
        buffer.Add(0, 0, 1, 2f);
        buffer.Add(0, 0, 2, 3f);
        buffer.Add(0, 0, 3, 4f);

        var array = buffer.ToArray();
        Assert.Single(array);

        var dest = new Half4[1];
        buffer.CopyTo(dest);
        Assert.Equal(array[0].PackedValue, dest[0].PackedValue);
    }

    [Fact]
    public void PackedBuffer_IsReadOnly_False() => Assert.False(new PackedBuffer<Half2>().IsReadOnly);

    [Fact]
    public void PackedBuffer_TotalComponentCount()
    {
        var buffer = new PackedBuffer<Half2>(3);
        buffer.Add(0, 0, 0, 0f);
        buffer.Add(0, 0, 1, 0f);
        buffer.Add(1, 0, 0, 0f);
        buffer.Add(1, 0, 1, 0f);
        buffer.Add(2, 0, 0, 0f);
        buffer.Add(2, 0, 1, 0f);
        Assert.Equal(3 * 1 * 2, buffer.TotalComponentCount);
    }

    [Fact]
    public void ShortN2_PackedValue_Roundtrip()
    {
        var p = new ShortN2(0x80008000u);
        var u = p.Unpack();
        Assert.Equal(-1f, u.X, Epsilon);
        Assert.Equal(-1f, u.Y, Epsilon);
    }

    [Fact]
    public void ByteN2_PackedValue_Roundtrip()
    {
        var p = new ByteN2((ushort)0x8080);
        var u = p.Unpack();
        Assert.Equal(-1f, u.X, Epsilon);
        Assert.Equal(-1f, u.Y, Epsilon);
    }

    [Fact]
    public void U565_PackedValue_KnownBits()
    {
        var p = new U565((ushort)0x7BEF);
        var u = p.Unpack();
        Assert.Equal(15f, u.X);
        Assert.Equal(31f, u.Y);
        Assert.Equal(15f, u.Z);
    }

    [Fact]
    public void UNibble4_PackedValue_KnownBits()
    {
        var p = new UNibble4((ushort)0x1234);
        var u = p.Unpack();
        Assert.Equal(4f, u.X);
        Assert.Equal(3f, u.Y);
        Assert.Equal(2f, u.Z);
        Assert.Equal(1f, u.W);
    }

    [Fact]
    public void Color_PackedValue_KnownArgb()
    {
        var p = new Color(0xFF884422u);
        var u = p.Unpack();
        Assert.True(MathF.Abs(u.X - (0x88 / 255f)) < Epsilon);
        Assert.True(MathF.Abs(u.Y - (0x44 / 255f)) < Epsilon);
        Assert.True(MathF.Abs(u.Z - (0x22 / 255f)) < Epsilon);
        Assert.True(MathF.Abs(u.W - (0xFF / 255f)) < Epsilon);
    }

    [Fact]
    public void UDecN4_PackedValue_Known()
    {
        var packed = 0b11_0000000000_0000000000_0000000000u;
        var p = new UDecN4(packed);
        var u = p.Unpack();
        Assert.Equal(0f, u.X, Epsilon);
        Assert.Equal(0f, u.Y, Epsilon);
        Assert.Equal(0f, u.Z, Epsilon);
        Assert.Equal(1f, u.W, Epsilon);
    }

    [Fact]
    public void PackedBuffer_Clamp_OutOfRange()
    {
        var buffer = new PackedBuffer<UByteN4>(4);
        buffer.Add(0, 0, 0, 5.0f);
        buffer.Add(0, 0, 1, -1.0f);
        buffer.Add(0, 0, 2, 0.5f);
        buffer.Add(0, 0, 3, 2.0f);

        Assert.Equal(1.0f, buffer.Get<float>(0, 0, 0), Epsilon);
        Assert.Equal(0.0f, buffer.Get<float>(0, 0, 1), Epsilon);
        Assert.Equal(0.5f, buffer.Get<float>(0, 0, 2), 0.01f);
        Assert.Equal(1.0f, buffer.Get<float>(0, 0, 3), Epsilon);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void PackedBuffer_Get_InvalidElement_Throws(int index)
    {
        var buffer = new PackedBuffer<Half2>(4);
        buffer.Add(0, 0, 0, 0f);
        buffer.Add(0, 0, 1, 0f);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Get<float>(index, 0, 0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void PackedBuffer_Get_InvalidComponent_Throws(int component)
    {
        var buffer = new PackedBuffer<Half4>(4);
        buffer.Add(0, 0, 0, 0f);
        buffer.Add(0, 0, 1, 0f);
        buffer.Add(0, 0, 2, 0f);
        buffer.Add(0, 0, 3, 0f);
        Assert.Throws<ArgumentOutOfRangeException>(() => buffer.Get<float>(0, 0, component));
    }

    [Fact]
    public void PackedBuffer_EnsureCapacity_Grows()
    {
        var buffer = new PackedBuffer<Half2>(2);
        buffer.Add(0, 0, 0, 1f);
        buffer.Add(0, 0, 1, 2f);
        buffer.Add(1, 0, 0, 3f);
        buffer.Add(1, 0, 1, 4f);
        buffer.Add(2, 0, 0, 5f);
        buffer.Add(2, 0, 1, 6f);

        Assert.Equal(3, buffer.ElementCount);
        Assert.Equal(5f, buffer.Get<float>(2, 0, 0), Epsilon);
    }
}
