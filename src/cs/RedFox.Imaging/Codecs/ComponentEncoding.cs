using System;
using System.Buffers.Binary;
using RedFox.Imaging.Primitives;

namespace RedFox.Imaging.Codecs;

internal static class ComponentEncoding
{
    public static ComponentKind GetKind(ImageFormat format) => format switch
    {
        ImageFormat.R8Snorm or ImageFormat.R8Sint or
        ImageFormat.R8G8Snorm or ImageFormat.R8G8Sint or
        ImageFormat.R16Snorm or ImageFormat.R16Sint or
        ImageFormat.R16G16Snorm or ImageFormat.R16G16Sint or
        ImageFormat.R16G16B16A16Snorm or ImageFormat.R16G16B16A16Sint or
        ImageFormat.R32Sint or ImageFormat.R32G32Sint or
        ImageFormat.R32G32B32Sint or ImageFormat.R32G32B32A32Sint => ComponentKind.Signed,

        ImageFormat.R16Float or ImageFormat.R16G16Float => ComponentKind.Half,

        ImageFormat.R32Typeless or ImageFormat.R32Float or ImageFormat.D32Float or
        ImageFormat.R32G32Typeless or ImageFormat.R32G32Float or
        ImageFormat.R32G32B32Typeless or ImageFormat.R32G32B32Float or
        ImageFormat.R32G32B32A32Typeless or ImageFormat.R32G32B32A32Float => ComponentKind.Float,

        _ => ComponentKind.Unsigned,
    };

    public static float Decode8(byte value, ComponentKind kind)
    {
        return kind == ComponentKind.Signed ? MathF.Max((sbyte)value / 127f, -1f) : value / 255f;
    }

    public static byte Encode8(float value, ComponentKind kind)
    {
        if (kind == ComponentKind.Signed)
            return (byte)(sbyte)MathF.Round(Math.Clamp(value, -1f, 1f) * 127f);

        return (byte)(Math.Clamp(value, 0f, 1f) * 255f + 0.5f);
    }

    public static float Decode16(ushort value, ComponentKind kind) => kind switch
    {
        ComponentKind.Half => (float)BitConverter.UInt16BitsToHalf(value),
        ComponentKind.Signed => MathF.Max((short)value / 32767f, -1f),
        _ => value / 65535f,
    };

    public static ushort Encode16(float value, ComponentKind kind) => kind switch
    {
        ComponentKind.Half => BitConverter.HalfToUInt16Bits((Half)value),
        ComponentKind.Signed => (ushort)(short)MathF.Round(Math.Clamp(value, -1f, 1f) * 32767f),
        _ => (ushort)(Math.Clamp(value, 0f, 1f) * 65535f + 0.5f),
    };

    public static float Decode32(ReadOnlySpan<byte> source, ComponentKind kind)
    {
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(source);

        return kind switch
        {
            ComponentKind.Float => BitConverter.UInt32BitsToSingle(value),
            ComponentKind.Signed => (float)Math.Max((int)value / (double)int.MaxValue, -1.0),
            _ => (float)(value / (double)uint.MaxValue),
        };
    }

    public static void Encode32(float value, Span<byte> destination, ComponentKind kind)
    {
        uint bits = kind switch
        {
            ComponentKind.Float => BitConverter.SingleToUInt32Bits(value),
            ComponentKind.Signed => (uint)(int)Math.Round(Math.Clamp(value, -1.0, 1.0) * int.MaxValue),
            _ => (uint)Math.Round(Math.Clamp(value, 0.0, 1.0) * uint.MaxValue),
        };

        BinaryPrimitives.WriteUInt32LittleEndian(destination, bits);
    }
}
