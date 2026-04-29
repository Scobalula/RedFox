using RedFox.Graphics2D;
using RedFox.Graphics3D.Rendering;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.DXGI;
using System;

namespace RedFox.Graphics3D.D3D11;

internal static class D3D11Helpers
{
    public static void ThrowIfFailed(int result, string operation)
    {
        if (result < 0)
        {
            throw new D3D11Exception($"{operation} failed with HRESULT 0x{result:X8}.");
        }
    }

    public static uint AlignTo16(int value)
    {
        int aligned = (value + 15) & ~15;
        return checked((uint)aligned);
    }

    public static Format GetDxgiFormat(ImageFormat format)
    {
        if (format == ImageFormat.Unknown)
        {
            return Format.FormatUnknown;
        }

        return format switch
        {
            ImageFormat.R8G8B8A8Unorm => Format.FormatR8G8B8A8Unorm,
            ImageFormat.R8G8B8A8UnormSrgb => Format.FormatR8G8B8A8UnormSrgb,
            ImageFormat.B8G8R8A8Unorm => Format.FormatB8G8R8A8Unorm,
            ImageFormat.B8G8R8A8UnormSrgb => Format.FormatB8G8R8A8UnormSrgb,
            ImageFormat.D32Float => Format.FormatD32Float,
            ImageFormat.D24UnormS8Uint => Format.FormatD24UnormS8Uint,
            _ => (Format)format,
        };
    }

    public static Format GetAttributeFormat(VertexAttribute attribute)
    {
        if (attribute.Type == VertexAttributeType.Float32)
        {
            return attribute.ComponentCount switch
            {
                1 => Format.FormatR32Float,
                2 => Format.FormatR32G32Float,
                3 => Format.FormatR32G32B32Float,
                4 => Format.FormatR32G32B32A32Float,
                _ => Format.FormatUnknown,
            };
        }

        if (attribute.Type == VertexAttributeType.UInt32)
        {
            return attribute.ComponentCount switch
            {
                1 => Format.FormatR32Uint,
                2 => Format.FormatR32G32Uint,
                3 => Format.FormatR32G32B32Uint,
                4 => Format.FormatR32G32B32A32Uint,
                _ => Format.FormatUnknown,
            };
        }

        if (attribute.Type == VertexAttributeType.Int32)
        {
            return attribute.ComponentCount switch
            {
                1 => Format.FormatR32Sint,
                2 => Format.FormatR32G32Sint,
                3 => Format.FormatR32G32B32Sint,
                4 => Format.FormatR32G32B32A32Sint,
                _ => Format.FormatUnknown,
            };
        }

        return Format.FormatUnknown;
    }

    public static string GetShaderProfile(ShaderStage stage)
    {
        return stage switch
        {
            ShaderStage.Vertex => "vs_5_0",
            ShaderStage.Fragment => "ps_5_0",
            ShaderStage.Compute => "cs_5_0",
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unsupported shader stage."),
        };
    }

    public static string GetSemanticName(VertexAttribute attribute, out uint semanticIndex)
    {
        string name = attribute.Name;
        if (name.Equals("Positions", StringComparison.Ordinal) || name.Equals("LineStart", StringComparison.Ordinal))
        {
            semanticIndex = 0;
            return "POSITION";
        }

        if (name.Equals("LineEnd", StringComparison.Ordinal))
        {
            semanticIndex = 1;
            return "POSITION";
        }

        if (name.Equals("Normals", StringComparison.Ordinal))
        {
            semanticIndex = 0;
            return "NORMAL";
        }

        if (name.Equals("Color", StringComparison.Ordinal))
        {
            semanticIndex = 0;
            return "COLOR";
        }

        if (name.Equals("Along", StringComparison.Ordinal))
        {
            semanticIndex = 0;
            return "TEXCOORD";
        }

        if (name.Equals("Side", StringComparison.Ordinal))
        {
            semanticIndex = 1;
            return "TEXCOORD";
        }

        if (name.Equals("WidthScale", StringComparison.Ordinal))
        {
            semanticIndex = 2;
            return "TEXCOORD";
        }

        semanticIndex = 0;
        return name.ToUpperInvariant();
    }

    public static Bool32 ToBool32(bool value) => value ? new Bool32(1) : new Bool32(0);
}
